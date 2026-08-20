package com.ferrarispos.manager

import android.os.Bundle
import androidx.activity.ComponentActivity
import androidx.activity.compose.setContent
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Modifier
import androidx.compose.ui.unit.dp
import retrofit2.Retrofit
import retrofit2.converter.gson.GsonConverterFactory
import retrofit2.http.GET
import retrofit2.http.Header

interface Api { @GET("api/dashboard") suspend fun dashboard(@Header("Authorization") auth:String): Dashboard }
data class Dashboard(val todaySales:Double=0.0,val todayTickets:Int=0,val cash:Double=0.0,val card:Double=0.0,val other:Double=0.0,val lowStockCount:Int=0,val inventoryValue:Double=0.0,val openCash:Double=0.0,val syncedAt:String?=null,val recentSales:List<Map<String,Any>>=emptyList(),val lowStock:List<Map<String,Any>>=emptyList(),val topProducts:List<Map<String,Any>>=emptyList(),val idleProducts:List<Map<String,Any>>=emptyList())

class MainActivity:ComponentActivity(){
 override fun onCreate(b:Bundle?){super.onCreate(b);setContent{FerrariApp()}}
}

@Composable fun FerrariApp(){
 var url by remember{mutableStateOf("https://TU-SERVIDOR/")}; var token by remember{mutableStateOf("")}; var d by remember{mutableStateOf<Dashboard?>(null)}; var error by remember{mutableStateOf("")}
 MaterialTheme { Surface(Modifier.fillMaxSize()){ LazyColumn(Modifier.padding(16.dp),verticalArrangement=Arrangement.spacedBy(12.dp)){
  item{Text("FerrariPOS Manager",style=MaterialTheme.typography.headlineMedium)}
  item{OutlinedTextField(url,{url=it},label={Text("URL de la API")},modifier=Modifier.fillMaxWidth())}
  item{OutlinedTextField(token,{token=it},label={Text("Token")},modifier=Modifier.fillMaxWidth())}
  item{Button(onClick={error=""; try{val api=Retrofit.Builder().baseUrl(if(url.endsWith("/"))url else "$url/").addConverterFactory(GsonConverterFactory.create()).build().create(Api::class.java); kotlinx.coroutines.GlobalScope.launch(kotlinx.coroutines.Dispatchers.Main){try{d=api.dashboard("Bearer $token")}catch(e:Exception){error=e.message?:("Error")}}}catch(e:Exception){error=e.message?:"URL inválida"}},modifier=Modifier.fillMaxWidth()){Text("Actualizar")}}
  if(error.isNotBlank()) item{Text(error,color=MaterialTheme.colorScheme.error)}
  d?.let{x->item{Metric("Ventas de hoy",money(x.todaySales),"Tickets: ${x.todayTickets}")};item{Row(Modifier.fillMaxWidth(),horizontalArrangement=Arrangement.spacedBy(8.dp)){MetricSmall("Efectivo",money(x.cash),Modifier.weight(1f));MetricSmall("Tarjetas",money(x.card),Modifier.weight(1f))}};item{Metric("Stock crítico",x.lowStockCount.toString(),"Inventario: ${money(x.inventoryValue)}")};item{Text("🏆 Más vendidos",style=MaterialTheme.typography.titleLarge)};items(x.topProducts.take(10)){Text("${it["description"] ?: "Producto"}  ·  ${it["quantity"] ?: 0}")};item{Text("⚠️ Stock bajo",style=MaterialTheme.typography.titleLarge)};items(x.lowStock.take(15)){Text("${it["description"] ?: "Producto"}  · stock ${it["stock"] ?: 0}  · proveedor ${it["supplier"] ?: "-"}")};item{Text("💤 Sin movimiento",style=MaterialTheme.typography.titleLarge)};items(x.idleProducts.take(15)){Text("${it["description"] ?: "Producto"}  · stock ${it["stock"] ?: 0}")}}
  }
 } } }
}
fun money(v:Double)="${'$'} ${"%,.2f".format(v)}"
@Composable fun Metric(title:String,value:String,sub:String)=Card(Modifier.fillMaxWidth()){Column(Modifier.padding(16.dp)){Text(title);Text(value,style=MaterialTheme.typography.headlineMedium);Text(sub)}}
@Composable fun MetricSmall(title:String,value:String,modifier:Modifier)=Card(modifier){Column(Modifier.padding(12.dp)){Text(title);Text(value)}}
