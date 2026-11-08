using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BS.Models;

public sealed class CellResult
{
    public int CellId { get; init; }
    public CellStatus FinalStatus { get; set; } = CellStatus.Pending;
    public DateTime StartedAt { get; init; }
    public DateTime? FinishedAt { get; set; }
    public string? ErrorMessage { get; set; }
}
