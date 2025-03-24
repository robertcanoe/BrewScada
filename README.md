![Captura de pantalla 2025-03-18 103651](https://github.com/user-attachments/assets/c496dad3-c8b9-4ab3-ab88-47626490a8c2)
# BrewScada
**BrewScada** es una aplicación desarrollada para la gestión automatizada de la producción de cerveza en una planta cervecera. Construida con **C#** y **WinForms**, simula y controla las etapas de producción (molienda, cocción, fermentación y embotellado), gestiona el inventario de materias primas y registra datos en una base de datos **MongoDB**. Incluye monitoreo en tiempo real, alertas de inventario y exportación de registros a PDF.

## Características

- **Gestión de Batches**: Inicia, pausa, detiene y simula hasta 10 lotes consecutivos con nombres únicos (ej. `Batch_0001`).
- **Simulación Acelerada**: 7.5 segundos equivalen a 1 hora real:
  - Molienda: 1 hora (7.5 s).
  - Cocción: 2 horas (15 s).
  - Fermentación: 96 horas (15 s, optimizado).
  - Embotellado: 4 horas (30 s).
- **Inventario**: Controla malta, agua, levadura, botellas y lúpulo con alertas de bajo stock y un factor de merma del 10%.
- **Monitoreo**: Barra de progreso y etiquetas en tiempo real para seguimiento de etapas e inventario.
- **Registros**: Almacena datos en MongoDB y exporta logs a PDF usando iTextSharp.
- **Interfaz**: UI intuitiva en WinForms con controles de inicio, pausa y detención.

## Requisitos

### Software
- **Sistema Operativo**: Windows 10 o superior.
- **.NET Framework**: 4.7.2 o superior.
- **MongoDB Server**: 4.0 o superior, configurado en `localhost:27017`.
- **Dependencias**:
  - `MongoDB.Driver` (instalable vía NuGet).
  - `iTextSharp` (instalable vía NuGet).

### Hardware
- **Procesador**: 1 GHz o superior.
- **RAM**: Mínimo 2 GB.
- **Espacio en Disco**: 500 MB (incluye MongoDB y datos).

## Instalación

1. **Clona el Repositorio**
   ```bash
   git clone https://github.com/<tu-usuario>/BrewScada.git
   cd BrewScada

2. **Instala Dependencias**
- Asegúrate de tener .NET Framework 4.7.2+ instalado.
- Descarga e instala MongoDB y configúralo en localhost:27017.
- Abre el proyecto en Visual Studio y restaura los paquetes NuGet:
```bash
dotnet restore
```
### 3. Compila y Ejecuta
- Compila el proyecto en Visual Studio (`Build > Build Solution`).
- Ejecuta `BrewScada.exe` desde `bin/Debug` o `bin/Release` con permisos de administrador.

### 4. Inicialización
- Al iniciar por primera vez, el inventario se carga con:
  - Malta: 2000 kg
  - Agua: 12000 L
  - Levadura: 7.5 kg
  - Botellas: 10000 unidades
  - Lúpulo: 30 kg

## Uso

### 1. Iniciar un Batch
- Haz clic en "Empezar" para comenzar un nuevo lote. Se generará un nombre como `Batch_0001`.
- Observa el progreso en la barra y las etiquetas de etapa.

### 2. Pausar o Detener
- Usa "Pausar" para suspender y "Continuar" para reanudar.
- "Detener" finaliza el batch actual completando las etapas restantes.

### 3. Monitorear Inventario
- Revisa las etiquetas de inventario. Si un recurso cae por debajo del umbral mínimo (ej. Malta < 200 kg), aparecerá una alerta en rojo.

### 4. Exportar Registros
- Haz clic en "Exportar a PDF" para generar un informe con los logs de producción.

## Estructura del Proyecto
- `Form1.cs` / `Form1.Designer.cs`: Lógica principal y diseño de la interfaz gráfica.
- `InventoryManager.cs`: Gestión del inventario (CRUD y alertas).
- `ProductionLog.cs`: Registro y exportación de logs a PDF.
- `MongoDBConnection.cs`: Conexión y operaciones con MongoDB.
- `Program.cs`: Punto de entrada de la aplicación.
- `Ingrediente.cs`: Modelos de datos (Ingrediente, Produccion, Counter).

## Detalles Técnicos
- **Simulación**: Usa timers (`_processTimer`, `_progressTimer`, `_delayTimer`) para simular el proceso:
  - `_processTimer.Interval = 7500` (7.5 s por hora).
  - `_delayTimer.Interval = 10000` (10 s de retraso tras embotellado).
- **Merma**: Factor de pérdida del 10% (`MERMA_FACTOR = 0.9`).
- **Base de Datos**: Colecciones en MongoDB:
  - `Ingredientes`: Materias primas.
  - `Produccion`: Logs de batches.
  - `Counters`: Contador de nombres de batches.
  - `BatchesBotellas`: Botellas producidas.

## Resolución de Problemas
- **"Un lote está ya en ejecución"**: Finaliza el batch actual con "Detener".
- **Batch no cambia de nombre**: Verifica que MongoDB esté activo en `localhost:27017`.
- **Error al exportar PDF**: Asegúrate de tener permisos de escritura en la carpeta destino.
