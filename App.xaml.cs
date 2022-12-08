﻿using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using NLog;

namespace NodoAme;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        DispatcherUnhandledException +=
            OnDispatcherUnhandledException;
        TaskScheduler.UnobservedTaskException +=
            OnUnobservedTaskException;
        AppDomain.CurrentDomain.UnhandledException +=
            OnUnhandledException;
    }

    private void OnDispatcherUnhandledException(
        object sender,
        DispatcherUnhandledExceptionEventArgs e)
    {
        var exception = e.Exception;
        HandleException(exception);
    }

    private void OnUnobservedTaskException(
        object sender,
        UnobservedTaskExceptionEventArgs e)
    {
        var exception = e.Exception.InnerException as Exception;
        HandleException(exception);
    }

    private void OnUnhandledException(
        object sender,
        UnhandledExceptionEventArgs e)
    {
        var exception = e.ExceptionObject as Exception;
        HandleException(exception);
    }

    private void HandleException(Exception? e)
    {
        MessageBox.Show(
            $"エラーが発生しました\n{e?.ToString()}"
        );
        var logger = LogManager.GetCurrentClassLogger();
        MessageBox.Show(
            $"エラーが発生しました。\n詳細：{e?.Message}",
            "エラーが発生",
            MessageBoxButton.OK,
            MessageBoxImage.Error
		);
        logger.Error($"Error!{e?.ToString()}");
        logger.Error($"{ e?.Message }");
        //Environment.Exit(1);
    }
}
