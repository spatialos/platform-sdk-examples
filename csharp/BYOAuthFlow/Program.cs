using System;
using System.IO;
using System.Linq;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Improbable.SpatialOS.Deployment.V1Alpha1;
using Improbable.SpatialOS.Platform.Common;
using Improbable.SpatialOS.PlayerAuth.V2Alpha1;
using Improbable.Worker;
using Improbable.Worker.Alpha;
using Utils;
using Deployment = Improbable.SpatialOS.Deployment.V1Alpha1.Deployment;
using Locator = Improbable.Worker.Alpha.Locator;
using LocatorParameters = Improbable.Worker.Alpha.LocatorParameters;

namespace BYOAuthFlow
{
    internal class Program
    {
        /// <summary>
        ///     PLEASE REPLACE ME.
        ///     Your SpatialOS project name.
        ///     It should be the same as the name specified in the local spatialos.json file used to start local API service.
        /// </summary>
        private const string ProjectName = "platform_sdk_examples";

        /// <summary>
        ///     PLEASE REPLACE ME.
        ///     The path to a valid launch configuration json file.
        /// </summary>
        private const string LaunchConfigFilePath = "../../blank_project/default_launch.json";

        /// <summary>
        ///     PLEASE REPLACE ME.
        ///     The assembly you want the cloud deployment to use.
        /// </summary>
        private const string AssemblyId = "blank_project";

        /// <summary>
        /// The local API service port to use for the local scenario.
        /// </summary>
        private const int localAPIServicePort = 9876;


        /// <summary>
        ///     PLEASE REPLACE ME.
        ///     The SpatialOS refresh token of a service account or a user account.
        /// </summary>
        private static string RefreshToken =>
            Environment.GetEnvironmentVariable("IMPROBABLE_REFRESH_TOKEN") ?? "PLEASE_REPLACE_ME";

        /// <summary>
        ///     This contains the implementation of the "integrate your own authentication provide" scenario.
        ///     1. Start a cloud deployment.
        ///     2. Generate a PlayerIdentityToken.
        ///     3. List deployments.
        ///     4. Generate a LoginToken for a selected deployment.
        ///     5. Connect to the deployment using the PlayerIdentityToken and the LoginToken.
        /// </summary>
        /// <param name="args"></param>
        private static void Main(string[] args)
        {
            Console.WriteLine("Starting cloud scenario");
            
            var platformCredentials = new PlatformRefreshTokenCredential(RefreshToken);
            RunScenario(
                "locator.improbable.io",
                443,
                PlayerIdentityTokenServiceClient.Create(credentials: platformCredentials),
                LoginTokenServiceClient.Create(credentials: platformCredentials),
                DeploymentServiceClient.Create(credentials: platformCredentials),
                insecureConnection: false);

            Console.WriteLine("Starting Local API service Scenario");
            var localAPIServiceEndpoint = new PlatformApiEndpoint("localhost", localAPIServicePort, true);
            RunScenario(
                "localhost",
                localAPIServicePort,
                PlayerIdentityTokenServiceClient.Create(localAPIServiceEndpoint),
                LoginTokenServiceClient.Create(localAPIServiceEndpoint),
                DeploymentServiceClient.Create(localAPIServiceEndpoint),
                insecureConnection: true);
        }

        private static void RunScenario(string locatorHost, ushort locatorPort, PlayerIdentityTokenServiceClient pitClient, LoginTokenServiceClient ltClient,
            DeploymentServiceClient dsClient, bool insecureConnection)
        {
            var deployment = Setup(dsClient);
            try
            {
                var pit = CreatePlayerIdentityToken(pitClient);
                deployment = FindDeployment(dsClient, deployment.Id);
                var loginToken = CreateLoginTokenForDeployment(ltClient, pit, deployment.Id);
                ConnectToLocator(locatorHost, locatorPort, insecureConnection, pit, loginToken);
            }
            finally
            {
                Cleanup(dsClient, deployment);
            }
        }

        private static string CreatePlayerIdentityToken(PlayerIdentityTokenServiceClient pitClient)
        {
            Console.WriteLine("Generate a PlayerIdentityToken");
            var playerIdentityTokenResponse = pitClient.CreatePlayerIdentityToken(
                new CreatePlayerIdentityTokenRequest
                {
                    Provider = "provider",
                    PlayerIdentifier = "player_identifier",
                    ProjectName = ProjectName,
                });
            return playerIdentityTokenResponse.PlayerIdentityToken;
        }

        private static Deployment FindDeployment(DeploymentServiceClient dsc, string deploymentId)
        {
            Console.WriteLine("Choosing deployment");
            return dsc.ListDeployments(new ListDeploymentsRequest
            {
                ProjectName = ProjectName
            }).First(d => d.Id == deploymentId);
        }

        private static string CreateLoginTokenForDeployment(LoginTokenServiceClient ltClient,
            string pit, string deploymentId)
        {
            Console.WriteLine("Generate a Login Token for the selected deployment");
            var createLoginTokenResponse = ltClient.CreateLoginToken(
                new CreateLoginTokenRequest
                {
                    PlayerIdentityToken = pit,
                    DeploymentId = deploymentId,
                    LifetimeDuration = Duration.FromTimeSpan(new TimeSpan(0, 0, 30, 0)),
                    WorkerType = "UnityClient",
                });
            var loginToken = createLoginTokenResponse.LoginToken;
            return loginToken;
        }

        private static void ConnectToLocator(string locatorHost, ushort locatorPort, bool insecureConnection, string pit, string loginToken)
        {
            Console.WriteLine("Connect to the deployment using the LoginToken and PlayerIdentityToken");
            var locatorParameters = new LocatorParameters
            {
                PlayerIdentity = new PlayerIdentityCredentials
                {
                    PlayerIdentityToken = pit,
                    LoginToken = loginToken,
                },
              UseInsecureConnection = insecureConnection,
            };

            var locator = new Locator(locatorHost, locatorPort, locatorParameters);
            var connectionParameters = CreateConnectionParameters();
            using (var connectionFuture = locator.ConnectAsync(connectionParameters))
            {
                var connectionOption = connectionFuture.Get(5000 /* Using milliseconds */);
                if (connectionOption.HasValue)
                {
                    var connection = connectionOption.Value;
                    Console.WriteLine("connected: {0}", connection.IsConnected);
                }
                else
                {
                    Console.WriteLine("connection returned nothing");
                }

            }
            
        }

        private static ConnectionParameters CreateConnectionParameters()
        {
            var connectionParameters = new ConnectionParameters();
            connectionParameters.WorkerType = "UnityClient";
            connectionParameters.Network.ConnectionType = NetworkConnectionType.Tcp;
            connectionParameters.Network.UseExternalIp = true;
            return connectionParameters;
        }

        private static Deployment Setup(DeploymentServiceClient dsc)
        {
            var launchConfig = File.ReadAllText(LaunchConfigFilePath);
            var deploymentName = $"byoauth_flow_{StringUtils.Random(6)}";
            Console.WriteLine($"Starting a deployment with name: {deploymentName}");

            var deployment = dsc.CreateDeployment(new CreateDeploymentRequest
            {
                Deployment = new Deployment
                {
                    ProjectName = ProjectName,
                    Name = deploymentName,
                    LaunchConfig = new LaunchConfig
                    {
                        ConfigJson = launchConfig
                    },
                    AssemblyId = AssemblyId,
                }
            }).PollUntilCompleted().GetResultOrNull();

            if (deployment.Status == Deployment.Types.Status.Error)
            {
                throw new Exception(
                    "Failed to start deployment; please make sure to build the project by running `spatial build` in the project directory");
            }

            return deployment;
        }

        /// <summary>
        ///     This stops the cloud deployments as a cleanup.
        /// </summary>
        private static void Cleanup(DeploymentServiceClient dsc, Deployment deployment)
        {
            if (deployment.Status == Deployment.Types.Status.Running ||
                deployment.Status == Deployment.Types.Status.Starting)
            {
                Console.WriteLine("Stopping deployment");

                dsc.StopDeployment(new StopDeploymentRequest
                {
                    Id = deployment.Id,
                    ProjectName = deployment.ProjectName
                });
            }
        }
    }
}