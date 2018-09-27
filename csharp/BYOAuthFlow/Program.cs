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
        ///     PlEASE REPLACE ME.
        ///     Your SpatialOS project name.
        ///     It should be the same as the name specified in the local spaitalos.json file used to start spatiald.
        /// </summary>
        private const string ProjectName = "platform_sdk_examples";

        /// <summary>
        ///     PlEASE REPLACE ME.
        ///     The path to a valid launch configuration json file.
        /// </summary>
        private const string LaunchConfigFilePath = "../../blank_project/default_launch.json";

        /// <summary>
        ///     PlEASE REPLACE ME.
        ///     The assembly you would want the cloud deployment to use.
        /// </summary>
        private const string AssemblyId = "blank_project";

        /// <summary>
        /// The Spatiald port to use for the local scenario
        /// </summary>
        private const int SpatialdPort = 9876;


        /// <summary>
        ///     PlEASE REPLACE ME.
        ///     The SpatialOS Platform refresh token of a service account or a user account.
        /// </summary>
        private static string RefreshToken =>
            Environment.GetEnvironmentVariable("IMPROBABLE_REFRESH_TOKEN") ?? "5b6c50ed-f488-45f2-b02b-e6caa346b26e";

        /// <summary>
        ///     This contains the implementation of the "bring your own auth flow" scenario.
        ///     1. Start a cloud deployment.
        ///     2. Generate a Player Identity Token.
        ///     3. List deployments.
        ///     4. Generate a Login Token for a selected deployment.
        ///     5. Connect to the deployment using the Player Identity Token and the Login Token.
        /// </summary>
        /// <param name="args"></param>
        private static void Main(string[] args)
        {
            Console.WriteLine("Starting Cloud Scenario");
            var platformCredentials = new PlatformRefreshTokenCredential(RefreshToken);
            RunScenario(
                "locator.improbable.io",
                PlayerIdentityTokenServiceClient.Create(credentials: platformCredentials),
                LoginTokenServiceClient.Create(credentials: platformCredentials),
                DeploymentServiceClient.Create(credentials: platformCredentials),
                insecureConnection: false);

//            Console.WriteLine("Starting Spatiald Scenario");
//            var spatialdEndpoint = new PlatformApiEndpoint("localhost", SpatialdPort, true);
//            RunScenario(
//                PlayerIdentityTokenServiceClient.Create(spatialdEndpoint),
//                LoginTokenServiceClient.Create(spatialdEndpoint),
//                DeploymentServiceClient.Create(spatialdEndpoint),
//                insecureConnection: true);
        }

        private static void RunScenario(string locatorHost, PlayerIdentityTokenServiceClient pitClient, LoginTokenServiceClient ltClient,
            DeploymentServiceClient dsClient, bool insecureConnection)
        {
            var deployment = Setup(dsClient);
            try
            {
                var pit = CreatePIT(pitClient);
                deployment = FindDeployment(dsClient, deployment.Id);
                var loginToken = CreateLoginTokenForDeployment(ltClient, pit, deployment.Id);
                ConnectToLocator(locatorHost, insecureConnection, pit, loginToken);
            }
            finally
            {
                Cleanup(dsClient, deployment);
            }
        }

        private static string CreatePIT(PlayerIdentityTokenServiceClient pitClient)
        {
            Console.WriteLine("Generate a PIT");
            var playerIdentityTokenResponse = pitClient.CreatePlayerIdentityToken(
                new CreatePlayerIdentityTokenRequest
                {
                    Provider = "provider",
                    PlayerIdentifier = "player_identifier",
                    ProjectName = ProjectName,
                });
            string pit = playerIdentityTokenResponse.PlayerIdentityToken;
            return pit;
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

        private static void ConnectToLocator(string locatorHost, bool insecureConnection, string pit, string loginToken)
        {
            Console.WriteLine("Connect to the deployment using the Login Token and PIT");
            var lp = new LocatorParameters
            {
                PlayerIdentity = new PlayerIdentityCredentials
                {
                    PlayerIdentityToken = pit,
                    LoginToken = loginToken,
                },
              UseInsecureConnection = insecureConnection,
            };

            var locator = new Locator(locatorHost, lp);
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
                    "Failed to create deployment; please make sure to build the project by running `spatial build` in the project directory");
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