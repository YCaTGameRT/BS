using BS.Infrastructure;
using BS.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BS.Services;

public sealed class CellProcessor
{
    private readonly StationContext _ctx;

    public CellProcessor(StationContext ctx) => _ctx = ctx;

    public void Process(object? state)
    {
        var cell = (Cell)state!;
        var token = _ctx.Token;
        var log = _ctx.Logger;
        var results = _ctx.Results;

        results.GetOrCreate(cell.Id);

        try
        {

            if (token.IsCancellationRequested)
            {
                log.Log(cell.Id, "Запуск отменён до начала обработки", CellStatus.Canceled);
                results.SetStatus(cell.Id, CellStatus.Canceled);
                return;
            }


            RunDiagnostics(cell, token);


            if (!RunCharging(cell, token))
                return;


            if (!RunCalibration(cell, token))
                return;


            results.SetStatus(cell.Id, CellStatus.Completed);
            log.Log(cell.Id, "Ячейка завершена", CellStatus.Completed);
        }
        catch (OperationCanceledException)
        {
            log.Log(cell.Id, "Операция отменена", CellStatus.Canceled);
            results.SetStatus(cell.Id, CellStatus.Canceled);
        }
        catch (Exception ex)
        {
            log.Log(cell.Id, $"Ошибка: {ex.Message}", CellStatus.Faulted);
            results.SetError(cell.Id, ex.Message);
            results.SetStatus(cell.Id, CellStatus.Faulted);
        }
        finally
        {

            try { _ctx.Countdown.Signal(); }
            catch (InvalidOperationException) {  }
        }
    }

    private void RunDiagnostics(Cell cell, CancellationToken token)
    {
        var log = _ctx.Logger;
        var results = _ctx.Results;

        results.SetStatus(cell.Id, CellStatus.Diagnostics);
        log.Log(cell.Id, "Диагностика начата", CellStatus.Diagnostics);

        long acc = 0;
        for (int i = 0; i < cell.DiagnosticIterations; i++)
        {

            acc += i ^ (i << 1);
            acc ^= (acc >> 3);


            if ((i & 0x3FF) == 0)
                token.ThrowIfCancellationRequested();
        }

        token.ThrowIfCancellationRequested();
        log.Log(cell.Id, "Диагностика завершена", CellStatus.Diagnostics);
    }

    private bool RunCharging(Cell cell, CancellationToken token)
    {
        var log = _ctx.Logger;
        var results = _ctx.Results;
        var sem = _ctx.ChargerSemaphore;

        results.SetStatus(cell.Id, CellStatus.WaitingForCharger);
        log.Log(cell.Id, "Ожидание слота зарядки", CellStatus.WaitingForCharger);

        bool acquired = false;
        try
        {

            sem.Wait(token);
            acquired = true;

            results.SetStatus(cell.Id, CellStatus.Charging);
            log.Log(cell.Id, "Слот зарядки получен / зарядка начата", CellStatus.Charging);

            
            bool interrupted = token.WaitHandle.WaitOne(cell.ChargeDurationMs);
            if (interrupted)
            {
                token.ThrowIfCancellationRequested();
            }

            results.IncSuccessfulCharge();
            log.Log(cell.Id, "Зарядка завершена", CellStatus.Charging);
            return true;
        }
        catch (OperationCanceledException)
        {
            if (!acquired)
            {
                results.IncCanceledChargerWait();
                log.Log(cell.Id, "Отмена во время ожидания слота зарядки", CellStatus.Canceled);
            }
            else
            {
                log.Log(cell.Id, "Зарядка прервана", CellStatus.Canceled);
            }
            results.SetStatus(cell.Id, CellStatus.Canceled);
            return false;
        }
        finally
        {
            if (acquired)
            {
                sem.Release();
                log.Log(cell.Id, "Слот зарядки освобождён", CellStatus.WaitingForCharger);
            }
        }
    }

    private bool RunCalibration(Cell cell, CancellationToken token)
    {
        var log = _ctx.Logger;
        var results = _ctx.Results;
        var mutex = _ctx.CalibrationMutex;

        results.SetStatus(cell.Id, CellStatus.WaitingForCalibration);
        log.Log(cell.Id, "Ожидание калибровочного стенда", CellStatus.WaitingForCalibration);

        bool acquired = false;
        try
        {

            var handles = new WaitHandle[] { mutex, token.WaitHandle };
            int index = WaitHandle.WaitAny(handles);

            if (index == WaitHandle.WaitTimeout || index == 1)
            {
                // Сработала отмена раньше мьютекса
                results.IncCanceledCalibrationWait();
                log.Log(cell.Id, "Отмена во время ожидания калибровочного стенда", CellStatus.Canceled);
                results.SetStatus(cell.Id, CellStatus.Canceled);
                return false;
            }


            acquired = true;
            if (token.IsCancellationRequested)
            {
                log.Log(cell.Id, "Отмена после получения мьютекса", CellStatus.Canceled);
                results.SetStatus(cell.Id, CellStatus.Canceled);
                return false;
            }

            results.SetStatus(cell.Id, CellStatus.Calibration);
            log.Log(cell.Id, "Калибровочный стенд получен / калибровка начата", CellStatus.Calibration);

            bool interrupted = token.WaitHandle.WaitOne(cell.CalibrationDurationMs);
            if (interrupted)
            {
                token.ThrowIfCancellationRequested();
            }

            results.IncSuccessfulCalibration();
            log.Log(cell.Id, "Калибровка завершена", CellStatus.Calibration);
            return true;
        }
        catch (OperationCanceledException)
        {
            log.Log(cell.Id, "Калибровка прервана", CellStatus.Canceled);
            results.SetStatus(cell.Id, CellStatus.Canceled);
            return false;
        }
        finally
        {
            if (acquired)
            {

                mutex.ReleaseMutex();
                log.Log(cell.Id, "Калибровочный стенд освобождён", CellStatus.WaitingForCalibration);
            }
        }
    }
}
