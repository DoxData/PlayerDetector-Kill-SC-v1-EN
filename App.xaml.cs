using System.Windows;
using System.Threading;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using System.Diagnostics; // Add this using directive

namespace PlayerDetector-Kill-SC-v1-EN;


public partial class App : Application
{
    private static Mutex? mutex = null; // Fix nullable warning

    
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        
        if (!AdminUtils.IsAdministrator())
        {
            var result = MessageBox.Show(
                "This application requires administrator permissions for certain functions.\n\nDo you want to restart it in administrator mode?",
                "Administrator permissions required",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning
            );

            if (result == MessageBoxResult.Yes)
            {
                AdminUtils.RestartAsAdministrator();
                Shutdown();
                return;
            }
            else
            {
                // Modo limitado
            }
        }

        const string appName = "PlayerDetector-Kill-SC-v1-EN";
        bool createdNew;

        mutex = new Mutex(true, appName, out createdNew);

        if (!createdNew)
        {
            // La aplicación ya está en ejecución
            Application.Current.Shutdown();
            return;
        }

        // Configurar logging detallado
        var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder
                .AddFilter("Microsoft", LogLevel.Warning)
                .AddFilter("System", LogLevel.Warning)
                .AddFilter("PlayerDetector", LogLevel.Debug)
                .AddConsole();
        });

        var logger = loggerFactory.CreateLogger<MainWindow>();

        try
        {
            var mainWindow = new MainWindow(logger);
            mainWindow.Show();
        }
        catch (Exception ex)
        {
            logger.LogCritical(ex, "Fatal error when starting the application");
            throw;
        }

        base.OnStartup(e);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        // Delete the PlayerDetector-Kill-SC.sys file from the application's root directory
        string filePath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "PlayerDetector-Kill-SC.sys");
        if (System.IO.File.Exists(filePath))
        {
            try
            {
                System.IO.File.Delete(filePath);
            }
            catch (Exception ex)
            {
                // Log the exception or handle it as needed
                Debug.WriteLine($"Error deleting file: {ex.Message}");
            }
        }

        mutex?.Dispose();
        base.OnExit(e);
    }

    // ...existing code...
}

// ...existing code...
// Copyright (c) 2024 DoxData. Exclusive ownership. 
// Prohibited use/modification without express authorization.
