Imports System.Threading
Imports Microsoft.Extensions.DependencyInjection
Imports Microsoft.Extensions.Hosting
Imports Microsoft.Extensions.Logging

Namespace Services
    ''' <summary>
    ''' ASP.NET Core BackgroundService that triggers the inventory release engine on a schedule.
    '''
    ''' Schedule:
    '''   - Immediately at application startup (catches any missed releases while server was offline)
    '''   - Then every hour indefinitely
    '''
    ''' All release logic lives in IInventoryReleaseEngine (scoped service).
    ''' This class is purely a scheduler that creates a DI scope and invokes the engine.
    ''' </summary>
    Public Class InventoryReleaseService
        Inherits BackgroundService

        Private ReadOnly _serviceProvider As IServiceProvider
        Private ReadOnly _logger As ILogger(Of InventoryReleaseService)

        ' Check every hour; the engine's condition ensures we only act when truly due
        Private ReadOnly _interval As TimeSpan = TimeSpan.FromHours(1)

        Public Sub New(serviceProvider As IServiceProvider,
                       logger As ILogger(Of InventoryReleaseService))
            _serviceProvider = serviceProvider
            _logger = logger
        End Sub

        Protected Overrides Async Function ExecuteAsync(stoppingToken As CancellationToken) As Task
            _logger.LogInformation("[InventoryReleaseService] Scheduled inventory-release job started.")

            ' Run immediately on startup, then repeat on interval
            Do
                Try
                    Using scope = _serviceProvider.CreateScope()
                        Dim engine = scope.ServiceProvider.GetRequiredService(Of IInventoryReleaseEngine)()
                        Dim released = Await engine.TriggerReleaseAsync()
                        If released > 0 Then
                            _logger.LogInformation("[InventoryReleaseService] Released inventory for {Count} request(s).", released)
                        End If
                    End Using
                Catch ex As Exception When Not TypeOf ex Is OperationCanceledException
                    _logger.LogError(ex, "[InventoryReleaseService] Unhandled error during release cycle.")
                End Try

                ' Wait for next cycle (exits cleanly when the app is shutting down)
                Try
                    Await Task.Delay(_interval, stoppingToken)
                Catch ex As TaskCanceledException
                    Exit Do
                End Try
            Loop

            _logger.LogInformation("[InventoryReleaseService] Scheduled inventory-release job stopped.")
        End Function
    End Class
End Namespace
