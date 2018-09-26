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
        ///     Please REPLACE ME.
        ///     Locator Host.
        /// </summary>
        private const string LocatorHost = "localhost";

        /// <summary>
        ///     PlEASE REPLACE ME.
        ///     The name of the deployment.
        /// </summary>
        private static readonly string DeploymentName = $"byoauth_flow_{StringUtils.Random(6)}";

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

        private static Deployment _deployment;

        private static readonly PlatformRefreshTokenCredential CredentialWithProvidedToken =
            new PlatformRefreshTokenCredential(RefreshToken);


        /// <summary>
        ///     PlEASE REPLACE ME.
        ///     The SpatialOS Platform refresh token of a service account or a user account.
        /// </summary>
        private static string RefreshToken =>
            Environment.GetEnvironmentVariable("IMPROBABLE_REFRESH_TOKEN") ?? "";

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
//                RunScenario(
//                        PlayerIdentityTokenServiceClient.Create(credentials: CredentialWithProvidedToken), 
//                        LoginTokenServiceClient.Create(credentials: CredentialWithProvidedToken),
//                        DeploymentServiceClient.Create(credentials: CredentialWithProvidedToken));

            var SpatialDPort = 1234;
            var spatialDPlatformApiEndponit = new PlatformApiEndpoint("localhost", SpatialDPort, true);
            RunScenario(
                PlayerIdentityTokenServiceClient.Create(spatialDPlatformApiEndponit),
                LoginTokenServiceClient.Create(spatialDPlatformApiEndponit),
                DeploymentServiceClient.Create(spatialDPlatformApiEndponit));
        }

        private static void RunScenario(PlayerIdentityTokenServiceClient pitClient, LoginTokenServiceClient ltClient,
            DeploymentServiceClient dsClient)
        {
            Setup(dsClient);

            try
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

                Console.WriteLine("Choosing deployment");
                var deployment = dsClient.ListDeployments(new ListDeploymentsRequest
                {
                    ProjectName = ProjectName
                }).First(d => d.Name == DeploymentName);

                Console.WriteLine("Generate a Login Token for the selected deployment");
                var createLoginTokenResponse = ltClient.CreateLoginToken(
                    new CreateLoginTokenRequest
                    {
                        PlayerIdentityToken = pit,
                        DeploymentId = deployment.Id,
                        LifetimeDuration = Duration.FromTimeSpan(new TimeSpan(0, 0, 30, 0)),
                        WorkerType = "UnityClient",
                    });
                var loginToken = createLoginTokenResponse.LoginToken;

                Console.WriteLine("Connect to the deployment {0} using the Login Token and PIT", DeploymentName);

                var lp = new LocatorParameters
                {
                    PlayerIdentity = new PlayerIdentityCredentials
                    {
                        PlayerIdentityToken = pit,
                        LoginToken = loginToken,
                    },
                    UseInsecureConnection = true,
                };

                var locator = new Locator(LocatorHost, lp);
                var connectionParameters = createConnectionParameters();
                var connectionFuture = locator.ConnectAsync(connectionParameters);

                var connectionOption = connectionFuture.Get(5000 /* Using milliseconds */);
                var connection = connectionOption.Value;
                Console.WriteLine("connected: {0}", connection.IsConnected);
            }
            finally
            {
                Cleanup(dsClient);
            }
        }

        private static ConnectionParameters createConnectionParameters()
        {
            var connectionParameters = new ConnectionParameters();
            connectionParameters.WorkerType = "UnityClient";
            connectionParameters.Network.ConnectionType = NetworkConnectionType.Tcp;
            connectionParameters.Network.UseExternalIp = true;
            return connectionParameters;
        }

        private static void Setup(DeploymentServiceClient dsc)
        {
            Console.WriteLine("Setting up for the scenario");
            Console.WriteLine("Starting a cloud deployment");
            var launchConfig = File.ReadAllText(LaunchConfigFilePath);
            _deployment = dsc.CreateDeployment(new CreateDeploymentRequest
            {
                Deployment = new Deployment
                {
                    ProjectName = ProjectName,
                    Name = DeploymentName,
                    LaunchConfig = new LaunchConfig
                    {
                        ConfigJson = launchConfig
                    },
                    AssemblyId = AssemblyId
                }
            }).PollUntilCompleted().GetResultOrNull();

            if (_deployment.Status == Deployment.Types.Status.Error)
            {
                throw new Exception(
                    "Failed to create the cloud deployment; please make sure to build the project by running `spatial build` in the project directory");
            }
        }

        /// <summary>
        ///     This stops the cloud deployments as a cleanup.
        /// </summary>
        private static void Cleanup(DeploymentServiceClient dsc)
        {
            if (_deployment.Status == Deployment.Types.Status.Running ||
                _deployment.Status == Deployment.Types.Status.Starting || 
                _deployment.Status == Deployment.Types.Status.Stopped)
            {
                Console.WriteLine("Stopping deployment");

                dsc.StopDeployment(new StopDeploymentRequest
                {
                    Id = _deployment.Id,
                    ProjectName = _deployment.ProjectName
                });
            }
        }
    }
}