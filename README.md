# Non-Blocking Window Clicker

Tired of automation tools that take over your mouse? This is a powerful WPF application designed for true background clicking. It sends commands directly to application windows, so you can keep working, gaming, or browsing without interruption.

> ### 🚀 Pre-compiled Version Available!
>
> You can download the ready-to-use `.exe` file directly from the **Releases** page:
> https://github.com/TheHolyOneZ/BackgroundClicker/releases
> No need to build it yourself.

![Application Screenshot](images/image.png)

---

## What It Does

This tool lets you automate mouse clicks at specific locations within a target application window. You can create simple auto-clickers or build powerful, multi-step sequences for complex tasks.

The key feature is its ability to work in the background. Unlike traditional macro tools that hijack your cursor, this application sends its commands directly, leaving your mouse free for you to use.

---

## Core Features

* **🖱️ True Background Clicks**
  Utilizes non-blocking Win32 API calls to click windows without moving your mouse.

* **💾 Profile System**
  Save and load different automation profiles. Each profile stores the target window, click coordinates, sequences, and all timing settings.

* **📜 Advanced Sequencing**
  Chain multiple clicks together. Each step can have unique coordinates and a specific delay, allowing for complex automation routines.

* **🤖 Humanization**
  Optional *Click Jitter* (a random offset) and *Randomized Interval* settings make automation appear more human-like.

* **🎯 Smart Targeting**
  An easy-to-use setup mode lets you capture a target window and coordinates by simply holding your mouse over it for 3 seconds.

* **⌨️ Hotkey Control**
  Start and stop the clicker instantly with a global **F8** hotkey, even when the application is minimized.

* **🧱 Blocking Mode**
  For applications that do not respond to background clicks, a blocking mode is available that temporarily moves the mouse to perform the click.

---

## How It Works

The application leverages the Windows operating system's own messaging system to communicate with other programs.

1. **Window Discovery**
   When you use the setup feature, the tool identifies the unique handle (`HWND`) of the target window under your cursor. This handle acts as a direct reference to that window.

2. **Sending Messages**
   In the default non-blocking mode, instead of moving your mouse, the tool sends `WM_LBUTTONDOWN` and `WM_LBUTTONUP` messages directly to the target window's handle. This tells the application that a click occurred at specific coordinates without involving your physical cursor.

3. **Persistence**
   All saved profiles, including window titles and sequences, are stored in a `profiles.xml` file located in:

   ```
   %AppData%\BackgroundClickerWpf
   ```

   This makes them available every time you launch the app.

---

## Quick Start Guide

1. Launch the application.
2. Open the program you want to automate clicks in.
3. Press **Start Setup (Hold 3s)**.
4. Move your mouse over the target window and hold it still for 3 seconds. The window title and coordinates will be filled automatically.
5. Enter a name in the **Profile** box and click **Save**.
6. Press the green **START** button or use the **F8** hotkey to begin.

---

## Gallery

### Main Interface

![Main Application Interface](images/image.png)

### How-To Guide

![How-To Guide](images/HowTo.png)

### Credits Screen

![Credits Screen](images/Credits.png)

---

## Standalone Auto-Clicker

In addition to the background clicker, the application includes a simple auto-clicker for tasks that require direct cursor control. You can open it from the main window.

### Auto-Clicker Features

* **Click Interval**
  Set a precise delay in milliseconds between each click.

* **Click Position**
  Choose between clicking at the current cursor location or a fixed screen position.

* **Mouse Button**
  Supports left, right, and middle mouse button clicks.

* **Click Type**
  Perform single or double clicks.

* **Hotkeys**

  * **Start/Stop**: Customizable hotkey (default **F9**) to toggle the clicker.
  * **Pick Position**: Fixed hotkey (**F10**) to capture the current mouse coordinates for fixed-position clicking.

* **Randomization**
  Option to slightly randomize the click interval to mimic human behavior.

* **Click Counter**
  Real-time counter to track the number of clicks performed.

---

## Notes

* Some applications, especially games with anti-cheat or custom input handling, may ignore background window messages. Use Blocking Mode in such cases.
* Always ensure automation complies with the terms of service of the software you are interacting with.
