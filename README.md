# YomiYa 📖

**YomiYa** es un lector de manga y cómics para escritorio, moderno y ligero, desarrollado con **Avalonia UI** y **.NET 10**. Su arquitectura está diseñada para ofrecer una experiencia fluida en PC, destacando por su sistema de plugins independientes y sincronización en la nube.

## ✨ Características Principales

* **Lector de Escritorio Nativo**: Optimizado para Windows y entornos de escritorio mediante el uso de `WinExe` y `net10.0`.
* **Arquitectura Decoupled**: Implementa el patrón **MVVM** utilizando el *CommunityToolkit.Mvvm* para una separación clara entre la lógica y la interfaz.
* **Sistema de Plugins EXE**: Los plugins (como *Akaya* o *NovelCool*) funcionan como procesos externos que se comunican vía TCP, evitando que un error en el plugin cierre la aplicación principal.
* **Sincronización con Google Drive**: Respalda tu biblioteca y progreso directamente en tu cuenta de Google usando el `appDataFolder`.
* **Personalización Visual**: Sistema dinámico de temas que incluye presets populares como *Catppuccin*, *Tokyo Night* y *Dracula*.
* **Inyección de Dependencias**: Gestión eficiente de servicios y ViewModels a través de un contenedor de servicios centralizado.

## 🛠️ Tecnologías Utilizadas

* **UI Framework**: [Avalonia UI 12.0.1](https://avaloniaui.net/) con soporte para `CompiledBindings`.
* **Estética**: [FluentAvalonia](https://github.com/amwx/FluentAvalonia) para una integración visual perfecta con el estilo moderno de Windows.
* **Persistencia**: SQLite para el manejo de la base de datos local e historial de lectura.
* **Resiliencia**: [Polly](https://github.com/App-vNext/Polly) para el manejo de reintentos en peticiones de red.

## 🚀 Estructura del Proyecto

El proyecto se organiza en tres pilares:

1.  **YomiYa (App)**: El ejecutable principal que contiene la interfaz de usuario y la lógica de navegación.
2.  **JXALib (Core)**: Librería base que define los contratos (`ISource`, `ICatalogueSource`) y modelos comunes.
3.  **Plugins**: Proyectos independientes que implementan la lógica de extracción (scraping) de sitios específicos.

## 🔧 Configuración para Desarrollo

### Requisitos
* [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0).
* Un archivo `credentials.json` en la carpeta del proyecto para habilitar las funciones de Google Drive.

### Instalación
1.  Clona el repositorio.
2.  Restaura las dependencias:
    ```bash
    dotnet restore
    ```
3.  Compila y ejecuta:
    ```bash
    dotnet run --project YomiYa/YomiYa.csproj
    ```

## 📄 Licencia

Este proyecto está bajo una licencia personalizada. Consulta el archivo [LICENSE](./LICENSE) para conocer los términos de uso y las restricciones de comercialización por terceros.
