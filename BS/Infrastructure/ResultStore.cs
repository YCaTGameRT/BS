using BS.Models;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BS.Infrastructure;

public sealed class ResultStore
{
    private readonly ConcurrentDictionary<int, CellResult> _results = new();

    private int _completed;
    private int _canceled;
    private int _faulted;
    private int _successfulCharges;
    private int _successfulCalibrations;
    private int _canceledChargerWaits;
    private int _canceledCalibrationWaits;

    public int Completed => Volatile.Read(ref _completed);
    public int Canceled => Volatile.Read(ref _canceled);
    public int Faulted => Volatile.Read(ref _faulted);
    public int SuccessfulCharges => Volatile.Read(ref _successfulCharges);
    public int SuccessfulCalibrations => Volatile.Read(ref _successfulCalibrations);
    public int CanceledChargerWaits => Volatile.Read(ref _canceledChargerWaits);
    public int CanceledCalibrationWaits => Volatile.Read(ref _canceledCalibrationWaits);

    public CellResult GetOrCreate(int cellId)
    {
        return _results.GetOrAdd(cellId, id => new CellResult
        {
            CellId = id,
            StartedAt = DateTime.Now
        });
    }

    public void SetStatus(int cellId, CellStatus status)
    {
        var r = GetOrCreate(cellId);

        if (status is CellStatus.Completed or CellStatus.Canceled or CellStatus.Faulted)
        {
            if (r.FinalStatus is CellStatus.Completed or CellStatus.Canceled or CellStatus.Faulted)
                return;
        }

        r.FinalStatus = status;

        switch (status)
        {
            case CellStatus.Completed:
                r.FinishedAt = DateTime.Now;
                Interlocked.Increment(ref _completed);
                break;
            case CellStatus.Canceled:
                r.FinishedAt = DateTime.Now;
                Interlocked.Increment(ref _canceled);
                break;
            case CellStatus.Faulted:
                r.FinishedAt = DateTime.Now;
                Interlocked.Increment(ref _faulted);
                break;
        }
    }

    public void SetError(int cellId, string message)
    {
        var r = GetOrCreate(cellId);
        r.ErrorMessage = message;
    }

    public void IncSuccessfulCharge() => Interlocked.Increment(ref _successfulCharges);
    public void IncSuccessfulCalibration() => Interlocked.Increment(ref _successfulCalibrations);
    public void IncCanceledChargerWait() => Interlocked.Increment(ref _canceledChargerWaits);
    public void IncCanceledCalibrationWait() => Interlocked.Increment(ref _canceledCalibrationWaits);
}
