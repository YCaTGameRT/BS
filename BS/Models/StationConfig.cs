using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BS.Models;

public sealed class StationConfig
{
    public int CellCount { get; init; } = 20;
    public int ChargerSlots { get; init; } = 4;
    public int DiagnosticIterations { get; init; } = 100_000;
    public int MinChargeMs { get; init; } = 300;
    public int MaxChargeMs { get; init; } = 900;
    public int MinCalibrationMs { get; init; } = 100;
    public int MaxCalibrationMs { get; init; } = 300;
    public int? AutoCancelMs { get; init; } = null;
}
