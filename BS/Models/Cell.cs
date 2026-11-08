using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BS.Models;

public sealed class Cell
{
    public int Id { get; init; }
    public int DiagnosticIterations { get; init; }
    public int ChargeDurationMs { get; init; }
    public int CalibrationDurationMs { get; init; }
}
