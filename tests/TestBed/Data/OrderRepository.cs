using System.Data;
using System.Data.Common;
using TestBed.Core;

namespace TestBed.Data;

// A lone repository with a hairy query builder and no peers to be measured against.
public class OrderRepository
{
    public string BuildQuery(NormalizationContext ctx, string carrier, DateTime? from,
                             DateTime? to, int? status, bool includeArchived)
    {
        var where = new List<string>();
        var sql = "SELECT * FROM Orders";

        if (!string.IsNullOrEmpty(ctx?.TenantId)) where.Add("TenantId = @tenant");
        if (!string.IsNullOrEmpty(carrier) && carrier != "ALL") where.Add("Carrier = @carrier");

        if (from != null && to != null) where.Add("Created BETWEEN @from AND @to");
        else if (from != null) where.Add("Created >= @from");
        else if (to != null) where.Add("Created <= @to");

        if (status != null)
        {
            switch (status.Value)
            {
                case 0: where.Add("Status IS NULL"); break;
                case 1: where.Add("Status = 'OPEN'"); break;
                case 2: where.Add("Status IN ('CLOSED','CANCELLED')"); break;
                default: where.Add("Status = @status"); break;
            }
        }

        if (!includeArchived) where.Add("Archived = 0");
        if (ctx != null && ctx.StrictMode && status == null) where.Add("Status IS NOT NULL");

        if (where.Count > 0) sql += " WHERE " + string.Join(" AND ", where);
        return sql;
    }

    public int CountOrders(IDbConnection connection, string tenantId)
    {
        using IDbCommand command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM Orders WHERE TenantId = @t";
        command.CommandType = CommandType.Text;
        return (int)command.ExecuteScalar();
    }
}
