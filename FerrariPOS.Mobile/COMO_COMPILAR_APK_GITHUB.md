# FerrariPOS Manager V2 — Compilar APK en GitHub

Esta versión incluye un workflow de GitHub Actions para compilar el APK automáticamente.

## 1. Crear un repositorio

En GitHub crea un repositorio nuevo, por ejemplo:

`FerrariPOS-Manager`

Puedes dejarlo privado.

## 2. Subir el contenido correcto

Sube **el contenido de la carpeta `FerrariPOS.Mobile`**, no la carpeta contenedora.

La estructura del repositorio debe quedar aproximadamente así:

```text
FerrariPOS-Manager/
├── Android/
├── API/
├── SyncAgent/
├── .github/
│   └── workflows/
│       └── build-apk.yml
└── ...
```

El workflow está dentro de:

`.github/workflows/android.yml`

## 3. Ejecutar la compilación

En GitHub entra en:

**Actions → FerrariPOS Manager - Build APK → Run workflow**

Espera a que termine el proceso.

## 4. Descargar el APK

Cuando aparezca el trabajo en verde:

**Actions → Build APK → Artifacts**

Descarga:

`FerrariPOS-Manager-debug`

Dentro estará:

`app-debug.apk`

Ese APK es para pruebas.

## Importante

Esta compilación genera una versión **debug**, no una versión firmada para publicar en Google Play.

Para una futura versión de distribución conviene crear un APK/AAB `release` firmado con una clave propia de FerrariPOS.

## Requisitos del proyecto

- Android Gradle Plugin 8.6.1
- Kotlin 2.0.21
- Gradle 8.7
- Java 17
- compileSdk 35
- minSdk 26

GitHub instala/configura estos componentes durante el workflow; no necesitas instalar Android Studio para esta compilación.
