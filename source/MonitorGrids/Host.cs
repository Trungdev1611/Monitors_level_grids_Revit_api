using Microsoft.Extensions.DependencyInjection;
using MonitorGrids.Views;
using MonitorGrids.ViewModels;
using MonitorGrids.Configuration;

namespace MonitorGrids;

/// <summary>
///     Provides a host for the application's services and manages their lifetimes
/// </summary>
public static class Host
{
    private static IServiceProvider? _serviceProvider;

    /// <summary>
    ///     Starts the host and configures the application's services
    /// </summary>
    public static void Start()
    {
        var services = new ServiceCollection();

        //Logging
        services.AddSerilog();

        //MVVM
        services.AddTransient<MonitorGridsViewModel>();
        services.AddTransient<MonitorGridsView>();

        _serviceProvider = services.BuildServiceProvider();
    }

    /// <summary>
    ///     Get service of type <typeparamref name="T"/>
    /// </summary>
    /// <typeparam name="T">The type of service object to get</typeparam>
    /// <exception cref="System.InvalidOperationException">There is no service of type <typeparamref name="T"/></exception>
    public static T GetService<T>() where T : class
    {
        if(_serviceProvider == null) Start(); //chạy qua command nếu không addin manager sẽ lỗi vì không chạy qua Application vì hàm Start k được chạy
        return _serviceProvider!.GetRequiredService<T>();
    }
}