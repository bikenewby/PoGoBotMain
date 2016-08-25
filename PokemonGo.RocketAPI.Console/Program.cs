#region

using System;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using PokemonGo.RocketAPI.Console.Xml;
using PokemonGo.RocketAPI.Exceptions;
using PokemonGo.RocketAPI.Logic.Logging;
using POGOProtos.Settings;

#endregion


namespace PokemonGo.RocketAPI.Console
{
    internal class Program
    {
        private static void Main()
        {
            var culture = CultureInfo.CreateSpecificCulture("en-US");

            CultureInfo.DefaultThreadCurrentCulture = culture;
            Thread.CurrentThread.CurrentCulture = culture;

            AppDomain.CurrentDomain.UnhandledException
                += delegate (object sender, UnhandledExceptionEventArgs eargs)
                {
                    Exception exception = (Exception)eargs.ExceptionObject;
                    System.Console.WriteLine(@"Unhandled exception: " + exception);
                    Environment.Exit(1);
                };

            ServicePointManager.ServerCertificateValidationCallback = Validator;
            Logger.SetLogger();

            if (File.Exists(XmlSettings._configsFile))
            {
                // Load the settings from the config file
                // If the current program is not the latest version, ensure we skip saving the file after loading
                // This is to prevent saving the file with new options at their default values so we can check for differences
                XmlSettings.LoadSettings();
                // After this point the object instantiated from Settings class will always use values from settings.xml file
            }
            else
            {
                // KS: Create new setting XML configuration file (case file not existed)
                XmlSettings.CreateSettings(new Settings());
            }

            Task.Run(() =>
            {
                try
                {
                    Settings botSetting = new Console.Settings();
                    BOTSessions multiSessionConfig = botSetting.MultiSessionsConfig;
                    //KS
                    if (botSetting.UseMultiSessions)
                    {
                        Logger.Write("Start running MULTI-SESSIONS using id: " + multiSessionConfig.SessionList[0].Uid);
                    } else
                    {
                        Logger.Write("Start running using id: " + botSetting.GoogleEmail);
                    }
                    //initialize session start time (in case not using MultiSessions)
                    Logic.Logic.sessionStartTime = DateTime.Now;
                    //---
                    new Logic.Logic(botSetting).Execute().Wait();
                }
                catch (PtcOfflineException)
                {
                    Logger.Write("PTC Servers are probably down OR your credentials are wrong. Try google", LogLevel.Error);
                    Logger.Write("Trying again in 60 seconds...");
                    Thread.Sleep(60000);
                    new Logic.Logic(new Settings()).Execute().Wait();
                }
                catch (AccountNotVerifiedException)
                {
                    Logger.Write("Account not verified. - Exiting");
                    Environment.Exit(0);
                }
                catch (Exception ex)
                {
                    Logger.Write($"Unhandled exception: {ex}", LogLevel.Error);
                    new Logic.Logic(new Settings()).Execute().Wait();
                }
            });
             System.Console.ReadLine();
        }

        public static bool Validator(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors) => true;
    }
}