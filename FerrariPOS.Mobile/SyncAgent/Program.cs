using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;

var cfg = JsonSerializer.Deserialize<Config>(await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory,"appsettings.json"))) ?? new();
var dbPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "FerrarisPOS", "FerrarisPOS.db");
Console.Title = "FerrariPOS Mobile Sync";
Console.WriteLine("FerrariPOS Mobile Sync Agent 1.0");
Console.WriteLine($"Base de datos: {dbPath}");
Console.WriteLine($"API: {cfg.ApiUrl}");
using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
while (true)
{
    try { var snapshot = BuildSnapshot(dbPath,cfg.CompanyId); using var req = new HttpRequestMessage(HttpMethod.Post,cfg.ApiUrl); req.Headers.Authorization=new AuthenticationHeaderValue("Bearer",cfg.ApiToken); req.Content=new StringContent(JsonSerializer.Serialize(snapshot),Encoding.UTF8,"application/json"); var res=await http.SendAsync(req); Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Sincronización: {(int)res.StatusCode} {res.ReasonPhrase}"); }
    catch(Exception ex){Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Error: {ex.Message}");}
    await Task.Delay(TimeSpan.FromSeconds(Math.Max(5,cfg.IntervalSeconds)));
}

static Snapshot BuildSnapshot(string path,string companyId)
{
    using var c=new SqliteConnection($"Data Source={path};Mode=ReadOnly;"); c.Open();
    decimal D(SqliteCommand q){return Convert.ToDecimal(q.ExecuteScalar()??0);}
    int I(SqliteCommand q){return Convert.ToInt32(q.ExecuteScalar()??0);}
    var today=DateTime.Now.ToString("yyyy-MM-dd");
    var sales=new List<RecentSale>();
    using(var q=c.CreateCommand()){q.CommandText="SELECT ticket_no,total,payment_method,COALESCE((SELECT full_name FROM users u WHERE u.id=s.user_id),''),created_at FROM sales s WHERE status='COMPLETED' ORDER BY id DESC LIMIT 50";using var r=q.ExecuteReader();while(r.Read())sales.Add(new((long)r.GetInt32(0),Convert.ToDecimal(r.GetValue(1)),r.GetString(2),r.GetString(3),DateTimeOffset.Parse(r.GetString(4))));}
    decimal todaySales, cash, card, other, inv, openCash; int tickets, low;
    using(var q=c.CreateCommand()){q.CommandText=$"SELECT COALESCE(SUM(total),0) FROM sales WHERE status='COMPLETED' AND date(created_at)=date('{today}')";todaySales=D(q);}
    using(var q=c.CreateCommand()){q.CommandText=$"SELECT COUNT(*) FROM sales WHERE status='COMPLETED' AND date(created_at)=date('{today}')";tickets=I(q);}
    using(var q=c.CreateCommand()){q.CommandText=$"SELECT COALESCE(SUM(CASE WHEN lower(payment_method)='efectivo' THEN total ELSE 0 END),0),COALESCE(SUM(CASE WHEN lower(payment_method) IN ('tarjeta','mercado pago','mercadopago') THEN total ELSE 0 END),0),COALESCE(SUM(CASE WHEN lower(payment_method) NOT IN ('efectivo','tarjeta','mercado pago','mercadopago') THEN total ELSE 0 END),0) FROM sales WHERE status='COMPLETED' AND date(created_at)=date('{today}')";using var r=q.ExecuteReader();r.Read();cash=Convert.ToDecimal(r.GetValue(0));card=Convert.ToDecimal(r.GetValue(1));other=Convert.ToDecimal(r.GetValue(2));}
    using(var q=c.CreateCommand()){q.CommandText="SELECT COALESCE(SUM(stock*cost_price),0) FROM products WHERE active=1";inv=D(q);}
    using(var q=c.CreateCommand()){q.CommandText="SELECT COALESCE(SUM(opening_amount),0) FROM cash_sessions WHERE status='OPEN'";openCash=D(q);}
    using(var q=c.CreateCommand()){q.CommandText="SELECT COUNT(*) FROM products WHERE active=1 AND stock<=min_stock";low=I(q);}
    var lows=new List<StockAlert>(); using(var q=c.CreateCommand()){q.CommandText="SELECT p.id,p.description,p.stock,p.min_stock,COALESCE(s.name,'') FROM products p LEFT JOIN supplier_products sp ON sp.product_id=p.id LEFT JOIN suppliers s ON s.id=sp.supplier_id WHERE p.active=1 AND p.stock<=p.min_stock ORDER BY p.stock ASC LIMIT 30";using var r=q.ExecuteReader();while(r.Read())lows.Add(new(r.GetInt64(0),r.GetString(1),Convert.ToDecimal(r.GetValue(2)),Convert.ToDecimal(r.GetValue(3)),r.GetString(4)));}
    var top=new List<TopProduct>(); using(var q=c.CreateCommand()){q.CommandText="SELECT si.product_id,si.description,SUM(si.quantity),SUM(si.total) FROM sale_items si JOIN sales s ON s.id=si.sale_id WHERE s.status='COMPLETED' AND date(s.created_at)=date('now') GROUP BY si.product_id,si.description ORDER BY SUM(si.quantity) DESC LIMIT 10";using var r=q.ExecuteReader();while(r.Read())top.Add(new(r.GetInt64(0),r.GetString(1),Convert.ToDecimal(r.GetValue(2)),Convert.ToDecimal(r.GetValue(3))));}
    var idle=new List<IdleProduct>(); using(var q=c.CreateCommand()){q.CommandText="SELECT p.id,p.description,p.stock,(SELECT MAX(s.created_at) FROM sale_items si JOIN sales s ON s.id=si.sale_id WHERE si.product_id=p.id AND s.status='COMPLETED') FROM products p WHERE p.active=1 AND p.stock>p.min_stock AND NOT EXISTS(SELECT 1 FROM sale_items si JOIN sales s ON s.id=si.sale_id WHERE si.product_id=p.id AND s.status='COMPLETED' AND s.created_at>=datetime('now','-30 day')) ORDER BY p.stock DESC LIMIT 20";using var r=q.ExecuteReader();while(r.Read())idle.Add(new(r.GetInt64(0),r.GetString(1),Convert.ToDecimal(r.GetValue(2)),r.IsDBNull(3)?null:DateTimeOffset.Parse(r.GetString(3))));}
    var moves=new List<StockMovement>(); using(var q=c.CreateCommand()){q.CommandText="SELECT sm.id,sm.product_id,p.description,sm.movement_type,sm.quantity,sm.reference,COALESCE(u.full_name,''),sm.created_at FROM stock_movements sm JOIN products p ON p.id=sm.product_id LEFT JOIN users u ON u.id=sm.user_id WHERE upper(sm.movement_type) IN ('AJUSTE','AJUSTE DE STOCK','AJUSTE_STOCK','CONTEO_FISICO','CONTEO FÍSICO','BAJA','SALIDA','AJUSTE_MANUAL') AND upper(COALESCE(sm.direction,''))='OUT' ORDER BY sm.id DESC LIMIT 50";using var r=q.ExecuteReader();while(r.Read())moves.Add(new(r.GetInt64(0),r.GetInt64(1),r.GetString(2),r.GetString(3),Convert.ToDecimal(r.GetValue(4)),r.GetString(5),r.GetString(6),DateTimeOffset.Parse(r.GetString(7))));}
    return new Snapshot(companyId,DateTimeOffset.UtcNow,new(todaySales,tickets,cash,card,other,low,inv,openCash),sales,lows,top,idle,moves);
}
record Config{public string ApiUrl{get;set;}="";public string ApiToken{get;set;}="";public string CompanyId{get;set;}="DEMO-001";public int IntervalSeconds{get;set;}=10;}
record Snapshot(string CompanyId,DateTimeOffset SyncedAt,Dashboard Dashboard,List<RecentSale> RecentSales,List<StockAlert> LowStock,List<TopProduct> TopProducts,List<IdleProduct> IdleProducts,List<StockMovement> StockMovements);
record Dashboard(decimal TodaySales,int TodayTickets,decimal Cash,decimal Card,decimal Other,int LowStockCount,decimal InventoryValue,decimal OpenCash);
record RecentSale(long Ticket,decimal Total,string PaymentMethod,string Cashier,DateTimeOffset CreatedAt);
record StockAlert(long ProductId,string Description,decimal Stock,decimal MinStock,string Supplier);
record TopProduct(long ProductId,string Description,decimal Quantity,decimal Total);
record IdleProduct(long ProductId,string Description,decimal Stock,DateTimeOffset? LastSaleAt);
record StockMovement(long Id,long ProductId,string Description,string MovementType,decimal Quantity,string Reference,string User,DateTimeOffset CreatedAt);
