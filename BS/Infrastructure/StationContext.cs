using BS.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace BS.Infrastructure;

public sealed class StationContext : IDisposable
{
    public StationConfig Config { get; }
    public SemaphoreSlim ChargerSemaphore { get; }
    public Mutex CalibrationMutex { get; }
    public CancellationToken Token { get; }
    public CancellationTokenSource Cts { get; }
    public CountdownEvent Countdown { get; }
    public ResultStore Results { get; }
    public ConsoleLogger Logger { get; }

    public StationContext(StationConfig config, CancellationTokenSource cts)
    {
        Config = config;
        Cts = cts;
        Token = cts.Token;
        ChargerSemaphore = new SemaphoreSlim(config.ChargerSlots, config.ChargerSlots);
        CalibrationMutex = new Mutex(initiallyOwned: false);
        Countdown = new CountdownEvent(config.CellCount);
        Results = new ResultStore();
        Logger = new ConsoleLogger();
    }

    public void Dispose()
    {
        ChargerSemaphore.Dispose();
        CalibrationMutex.Dispose();
        Countdown.Dispose();
        Cts.Dispose();
    }
}
