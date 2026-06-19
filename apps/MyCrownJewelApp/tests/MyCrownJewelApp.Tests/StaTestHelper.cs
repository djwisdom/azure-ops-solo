using System;
using System.Threading;
using MyCrownJewelApp.Pfpad;

namespace MyCrownJewelApp.Tests;

internal static class StaHelper
{
    public static void Run(Action<Form1> action)
    {
        Exception? ex = null;

        var thread = new Thread(() =>
        {
            try
            {
                using var form = new Form1();
                form.CreateControl();
                action(form);
            }
            catch (Exception e)
            {
                ex = e;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join(TimeSpan.FromSeconds(30));
        if (ex is not null)
        {
            throw ex;
        }
    }
}
