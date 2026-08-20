using Microsoft.Data.Sqlite;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls(Environment.GetEnvironmentVariable("ASPNETCORE_URLS") ?? "http://0.0.0.0:5080");
builder.Services.AddSingleton<MobileDb>();
var app = builder.Build();
var db = app.Services.GetRequiredService<MobileDb>();
db.Initialize();

bool Authorized(HttpRequest request, IConfiguration cfg) =>
    request.Headers.Authorization.ToString() == $"Bearer {cfg["FerrariPOS:ApiToken"]}";

app.MapGet("/health", () => Results.Ok(new { ok = true, service = "FerrariPOS Mobile API", version = "1.0" }));

app.MapPost("/api/sync/snapshot", async (HttpRequest req, Snapshot snapshot, IConfiguration cfg, MobileDb database) =>
{
    if (!Authorized(req, cfg)) return Results.Unauthorized();
    if (snapshot.CompanyId != cfg["FerrariPOS:CompanyId"]) return Results.BadRequest("Empresa no autorizada.");
    await database.ReplaceSnapshotAsync(snapshot);
    return Results.Ok(new { ok = true, syncedAt = DateTimeOffset.UtcNow });
});

app.MapGet("/api/dashboard", async (HttpRequest req, IConfiguration cfg, MobileDb database) =>
{
    if (!Authorized(req, cfg)) return Results.Unauthorized();
    var data = await database.GetDashboardAsync();
    return Results.Ok(data);
});

app.MapGet("/api/sales/recent", async (HttpRequest req, IConfiguration cfg, MobileDb database) =>
{
    if (!Authorized(req, cfg)) return Results.Unauthorized();
    return Results.Ok(await database.GetRecentSalesAsync());
});

app.Run();

public record Snapshot(string CompanyId, DateTimeOffset SyncedAt, Dashboard Dashboard, List<RecentSale> RecentSales, List<StockAlert> LowStock, List<TopProduct> TopProducts, List<IdleProduct> IdleProducts, List<StockMovement> StockMovements);
public record Dashboard(decimal TodaySales, int TodayTickets, decimal Cash, decimal Card, decimal Other, int LowStockCount, decimal InventoryValue, decimal OpenCash);
public record RecentSale(long Ticket, decimal Total, string PaymentMethod, string Cashier, DateTimeOffset CreatedAt);
public record StockAlert(long ProductId, string Description, decimal Stock, decimal MinStock, string Supplier);
public record TopProduct(long ProductId, string Description, decimal Quantity, decimal Total);
public record IdleProduct(long ProductId, string Description, decimal Stock, DateTimeOffset? LastSaleAt);
public record StockMovement(long Id, long ProductId, string Description, string MovementType, decimal Quantity, string Reference, string User, DateTimeOffset CreatedAt);

sealed class MobileDb
{
    readonly string _cs;
    public MobileDb(IConfiguration cfg) { _cs = $"Data Source={Path.Combine(AppContext.BaseDirectory, cfg["FerrariPOS:Database"] ?? "ferrari_mobile.db")}"; }
    SqliteConnection Open() { var c = new SqliteConnection(_cs); c.Open(); return c; }
    public void Initialize()
    {
        using var c = Open(); using var cmd = c.CreateCommand();
        cmd.CommandText = @"
CREATE TABLE IF NOT EXISTS snapshot(id INTEGER PRIMARY KEY CHECK(id=1), synced_at TEXT NOT NULL, today_sales REAL, today_tickets INTEGER, cash REAL, card REAL, other REAL, low_stock_count INTEGER, inventory_value REAL, open_cash REAL);
CREATE TABLE IF NOT EXISTS recent_sales(ticket INTEGER PRIMARY KEY, total REAL, payment_method TEXT, cashier TEXT, created_at TEXT);
CREATE TABLE IF NOT EXISTS low_stock(product_id INTEGER PRIMARY KEY, description TEXT, stock REAL, min_stock REAL, supplier TEXT);
CREATE TABLE IF NOT EXISTS top_products(product_id INTEGER PRIMARY KEY, description TEXT, quantity REAL, total REAL);
CREATE TABLE IF NOT EXISTS idle_products(product_id INTEGER PRIMARY KEY, description TEXT, stock REAL, last_sale_at TEXT);
CREATE TABLE IF NOT EXISTS stock_movements(id INTEGER PRIMARY KEY, product_id INTEGER, description TEXT, movement_type TEXT, quantity REAL, reference TEXT, user_name TEXT, created_at TEXT);";
        cmd.ExecuteNonQuery();
    }
    public async Task ReplaceSnapshotAsync(Snapshot s)
    {
        using var c = Open(); using var tx = c.BeginTransaction();
        Exec(c, tx, "DELETE FROM snapshot; DELETE FROM recent_sales; DELETE FROM low_stock; DELETE FROM top_products; DELETE FROM idle_products; DELETE FROM stock_movements;");
        Exec(c, tx, "INSERT INTO snapshot VALUES(1,$t,$a,$b,$c,$d,$e,$f,$g,$h)", ("$t", s.SyncedAt.ToString("O")), ("$a",s.Dashboard.TodaySales),("$b",s.Dashboard.TodayTickets),("$c",s.Dashboard.Cash),("$d",s.Dashboard.Card),("$e",s.Dashboard.Other),("$f",s.Dashboard.LowStockCount),("$g",s.Dashboard.InventoryValue),("$h",s.Dashboard.OpenCash));
        foreach (var x in s.RecentSales) Exec(c,tx,"INSERT INTO recent_sales VALUES($a,$b,$c,$d,$e)",("$a",x.Ticket),("$b",x.Total),("$c",x.PaymentMethod),("$d",x.Cashier),("$e",x.CreatedAt.ToString("O")));
        foreach (var x in s.LowStock) Exec(c,tx,"INSERT INTO low_stock VALUES($a,$b,$c,$d,$e)",("$a",x.ProductId),("$b",x.Description),("$c",x.Stock),("$d",x.MinStock),("$e",x.Supplier));
        foreach (var x in s.TopProducts) Exec(c,tx,"INSERT INTO top_products VALUES($a,$b,$c,$d)",("$a",x.ProductId),("$b",x.Description),("$c",x.Quantity),("$d",x.Total));
        foreach (var x in s.IdleProducts) Exec(c,tx,"INSERT INTO idle_products VALUES($a,$b,$c,$d)",("$a",x.ProductId),("$b",x.Description),("$c",x.Stock),("$d",x.LastSaleAt?.ToString("O")));
        foreach (var x in s.StockMovements) Exec(c,tx,"INSERT INTO stock_movements VALUES($a,$b,$c,$d,$e,$f,$g,$h)",("$a",x.Id),("$b",x.ProductId),("$c",x.Description),("$d",x.MovementType),("$e",x.Quantity),("$f",x.Reference),("$g",x.User),("$h",x.CreatedAt.ToString("O")));
        tx.Commit(); await Task.CompletedTask;
    }
    public async Task<object> GetDashboardAsync()
    {
        using var c=Open(); var d=new Dictionary<string,object?>(); using var q=c.CreateCommand(); q.CommandText="SELECT today_sales,today_tickets,cash,card,other,low_stock_count,inventory_value,open_cash,synced_at FROM snapshot WHERE id=1"; using var r=q.ExecuteReader();
        if(r.Read()){ d["todaySales"]=Convert.ToDecimal(r.GetValue(0)); d["todayTickets"]=r.GetInt32(1); d["cash"]=Convert.ToDecimal(r.GetValue(2)); d["card"]=Convert.ToDecimal(r.GetValue(3)); d["other"]=Convert.ToDecimal(r.GetValue(4)); d["lowStockCount"]=r.GetInt32(5); d["inventoryValue"]=Convert.ToDecimal(r.GetValue(6)); d["openCash"]=Convert.ToDecimal(r.GetValue(7)); d["syncedAt"]=r.GetString(8);} else return new { todaySales=0, todayTickets=0, cash=0, card=0, other=0, lowStockCount=0, inventoryValue=0, openCash=0, syncedAt=(string?)null };
        d["recentSales"]=await GetRecentSalesAsync(); d["lowStock"]=await GetRowsAsync("low_stock","product_id,description,stock,min_stock,supplier"); d["topProducts"]=await GetRowsAsync("top_products","product_id,description,quantity,total"); d["idleProducts"]=await GetRowsAsync("idle_products","product_id,description,stock,last_sale_at"); d["stockMovements"]=await GetRowsAsync("stock_movements","id,product_id,description,movement_type,quantity,reference,user_name,created_at"); return d;
    }
    public async Task<List<object>> GetRecentSalesAsync() => await GetRowsAsync("recent_sales","ticket,total,payment_method,cashier,created_at");
    async Task<List<object>> GetRowsAsync(string table,string cols){using var c=Open();using var q=c.CreateCommand();q.CommandText=$"SELECT {cols} FROM {table} ORDER BY rowid DESC LIMIT 50";using var r=q.ExecuteReader();var list=new List<object>();while(r.Read()){var o=new Dictionary<string,object?>();for(int i=0;i<r.FieldCount;i++)o[r.GetName(i)]=r.IsDBNull(i)?null:r.GetValue(i);list.Add(o);}await Task.CompletedTask;return list;}
    static void Exec(SqliteConnection c,SqliteTransaction tx,string sql,params (string,object?)[] ps){using var q=c.CreateCommand();q.Transaction=tx;q.CommandText=sql;foreach(var p in ps)q.Parameters.AddWithValue(p.Item1,p.Item2??DBNull.Value);q.ExecuteNonQuery();}
}
