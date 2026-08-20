# FerrariPOS Manager V3 - primera conexión real

Esta versión conecta el FerrariPOS de Windows con una API local y la app Android.

## Flujo
FerrariPOS -> SyncAgent -> API -> Android

## Datos sincronizados
- Ventas del día y cantidad de tickets.
- Medios de pago resumidos.
- Caja abierta.
- Valor de inventario.
- Stock crítico.
- Top de productos vendidos.
- Productos sin movimiento.
- Movimientos de inventario de salida por AJUSTE/CONTEO FISICO/BAJA/SALIDA, sin mostrar ventas como movimientos de inventario.

## Importante
La app principal FerrariPOS sigue funcionando de forma local. La sincronización es complementaria.
