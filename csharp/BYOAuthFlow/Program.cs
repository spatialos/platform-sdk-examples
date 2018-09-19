using System;
using System.IO;
using System.Linq;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Improbable.SpatialOS.Deployment.V1Alpha1;
using Improbable.SpatialOS.Platform.Common;
using Improbable.SpatialOS.PlayerAuth.V2Alpha1;
using Improbable.Worker;
using Utils;
using Deployment = Improbable.SpatialOS.Deployment.V1Alpha1.Deployment;

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

        private static readonly DeploymentServiceClient DeploymentServiceClient =
            DeploymentServiceClient.Create(credentials: CredentialWithProvidedToken);
        
        private static readonly LoginTokenServiceClient LoginTokenServiceClient =
            LoginTokenServiceClient.Create(credentials: CredentialWithProvidedToken);
        
        private static readonly PlayerIdentityTokenServiceClient PlayerIdentityTokenServiceClient =
            PlayerIdentityTokenServiceClient.Create(credentials: CredentialWithProvidedToken);

        /// <summary>
        ///     PlEASE REPLACE ME.
        ///     The SpatialOS Platform refresh token of a service account or a user account.
        /// </summary>
        private static string RefreshToken =>
            Environment.GetEnvironmentVariable("IMPROBABLE_REFRESH_TOKEN") ?? "PLEASE_REPLACE_ME";

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
            Setup();
            
            Console.WriteLine("Generate a PIT");
            var playerIdentityToken = PlayerIdentityTokenServiceClient.CreatePlayerIdentityToken(
                new CreatePlayerIdentityTokenRequest
                {
                    Provider = "provider",
                    PlayerIdentifier = "player_identifier",
                    ProjectName = ProjectName,
                    LifetimeDuration = Duration.FromTimeSpan(new TimeSpan(0, 1, 0, 0)),
                    Metadata = ByteString.CopyFromUtf8("metadata")
                }).PlayerIdentityToken;
            
            Console.WriteLine("Choose a deployment");
            var deployment  = DeploymentServiceClient.ListDeployments(
                new ListDeploymentsRequest
                {
                    ProjectName = ProjectName
                }
            ).First();
            
            Console.WriteLine("Generate a Login Token for the selected deployment");
            LoginTokenServiceClient.CreateLoginToken(
                new CreateLoginTokenRequest
                {
                    PlayerIdentityToken = playerIdentityToken,
                    DeploymentId = deployment.Id,
                    LifetimeDuration = Duration.FromTimeSpan(new TimeSpan(0, 0, 30, 0)),
                });
            
            Console.WriteLine("Connect to the deployment using the Login Token and PIT");
            LocatorParameters locatorParameters = new LocatorParameters();
            locatorParameters.ProjectName = ProjectName;
            locatorParameters.CredentialsType = LocatorCredentialsType.LoginToken;
            locatorParameters.LoginToken.Token = playerIdentityToken;
            var locator = new Locator("locator.improbable.io", locatorParameters);
            var connectionParameters = new ConnectionParameters();
            Func<QueueStatus, bool> queueCallback = delegate(QueueStatus status) { return true; };
            var connectionFuture = locator.ConnectAsync(DeploymentName, connectionParameters, queueCallback);
            var connectionOption = connectionFuture.Get(5000 /* Using milliseconds */);
            var connection = connectionOption.Value;
            Console.WriteLine(String.Format("connected: {}", connection.IsConnected));
            
            Cleanup();
        }

        /// <summary>
        /// TODO(nik): summarise
        /// </summary>
        private static void Setup()
        {
            Console.WriteLine("Setting up for the scenario");
            Console.WriteLine("Starting a cloud deployment");
            var launchConfig = File.ReadAllText(LaunchConfigFilePath);
            _deployment = DeploymentServiceClient.CreateDeployment(new CreateDeploymentRequest
            {
                Deployment = new Deployment
                {
                    ProjectName = ProjectName,
                    Name = DeploymentName,
                    LaunchConfig = new LaunchConfig
                    {
                        ConfigJson = launchConfig
                    },
                    AssemblyId =  AssemblyId
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
        private static void Cleanup()
        {
            Console.WriteLine("Cleaning up");
            DeploymentServiceClient.StopDeployment(new StopDeploymentRequest
            {
                Id = _deployment.Id,
                ProjectName = _deployment.ProjectName
            });
        }
    }
}