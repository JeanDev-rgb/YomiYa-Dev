# YomiYa 📖

> Un lector de manga y cómics para escritorio, rápido, elegante y extensible, construido con **Avalonia UI** y **.NET 10**.

---

<div align="center">

![Windows](https://img.shields.io/badge/Windows-Supported-0078D6?style=for-the-badge&logo=windows)
![Linux](https://img.shields.io/badge/Linux-Supported-FCC624?style=for-the-badge&logo=linux&logoColor=black)
![macOS](https://img.shields.io/badge/macOS-Supported-000000?style=for-the-badge&logo=apple)

![Framework](https://img.shields.io/badge/.NET-10-512BD4?style=for-the-badge)
![UI](https://img.shields.io/badge/Avalonia-UI-orange?style=for-the-badge)
![Architecture](https://img.shields.io/badge/Architecture-MVVM-00C896?style=for-the-badge)

</div>

---

## ✨ Características

### 🖥️ Experiencia de Escritorio Nativa
Desarrollado con **Avalonia UI** para ofrecer una experiencia moderna y fluida en:

- Windows
- Linux
- macOS

Compatible con arquitecturas modernas y optimizado para lectura prolongada.

---

### 🔌 Sistema de Plugins Aislados
Cada fuente funciona como un proceso independiente (`EXE`/binario) conectado vía TCP.

Esto permite:
- Evitar crashes globales.
- Actualizar plugins sin tocar el core.
- Mejor aislamiento y estabilidad.
- Desarrollo modular y escalable.

---

### ☁️ Sincronización en la Nube
Integra respaldo automático mediante **Google Drive** usando `appDataFolder`.

Sincroniza:
- Biblioteca
- Historial de lectura
- Progreso
- Configuraciones

---

### 🎨 Personalización Visual
Motor dinámico de temas inspirado en setups modernos.

Incluye presets como:
- Catppuccin
- Tokyo Night
- Dracula
- Nord
- Gruvbox

---

### 🧩 Arquitectura Moderna
Implementa:
- Patrón **MVVM**
- **Dependency Injection**
- Navegación desacoplada
- Servicios centralizados
- ViewModels reactivos con `CommunityToolkit.Mvvm`

---

# 🛠️ Stack Tecnológico

| Área | Tecnología |
|---|---|
| UI Framework | Avalonia UI 12 |
| Diseño Fluent | FluentAvalonia |
| Lenguaje | C# / .NET 10 |
| Base de Datos | SQLite |
| Networking | TCP Sockets |
| Resiliencia | Polly |
| Arquitectura | MVVM |
| Sincronización | Google Drive API |

---

# 🧱 Arquitectura del Proyecto

```txt
YomiYa
│
├── YomiYa/          → Aplicación principal
├── JXALib/          → Core y contratos compartidos
└── Plugins/         → Fuentes externas desacopladas
```

---

## 📦 Componentes

### 🧠 YomiYa
Aplicación principal.

Contiene:
- UI
- Navegación
- Gestión de biblioteca
- Configuración
- Comunicación con plugins

---

### 📚 JXALib
Librería compartida que define:
- Contratos
- Interfaces
- Modelos base
- Comunicación común

Ejemplos:

```csharp
ISource
ICatalogueSource
```

---

### 🔌 Plugins
Módulos independientes encargados del scraping y extracción de contenido.

Ejemplos:
- Akaya
- NovelCool

Cada plugin:
- Corre en su propio proceso.
- Se comunica vía TCP.
- Puede actualizarse independientemente.

---

# 🚀 Inicio Rápido

## 📋 Requisitos

- .NET 10 SDK
- Windows, Linux o macOS
- `credentials.json` para Google Drive

---

## ⚙️ Instalación

### 1️⃣ Clonar repositorio

```bash
git clone https://github.com/JeanDev-rgb/YomiYa-Dev.git
```

### 2️⃣ Restaurar dependencias

```bash
dotnet restore
```

### 3️⃣ Ejecutar proyecto

```bash
dotnet run --project YomiYa/YomiYa.csproj
```

---

# 🎨 Filosofía de Diseño

YomiYa busca combinar:

- ⚡ Rendimiento nativo
- 🧩 Modularidad extrema
- 🎨 Interfaces modernas
- ☁️ Sincronización transparente
- 📖 Experiencia de lectura limpia

Todo sin depender de Electron ni tecnologías web pesadas.

---

# 📸 Preview

```txt
(Próximamente screenshots aquí 👀)
```

---

# 📄 Licencia

Este proyecto utiliza una licencia personalizada.

Consulta el archivo: [Licencia Completa aquí](./LICENSE).

para conocer:
- Restricciones comerciales
- Distribución
- Uso de terceros

---

<div align="center">

### ⭐ Si te gusta YomiYa, dale una estrella al proyecto ⭐

</div>
