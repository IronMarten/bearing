using System.Data;
using System.Data.Common;

namespace TestBed.Data;

// Trivial sibling. Two repositories is not a cohort.
public class RateRepository
{
    public decimal GetBaseRate(string carrier)
    {
        return carrier == "UNKNOWN" ? 0m : 10m;
    }
}
