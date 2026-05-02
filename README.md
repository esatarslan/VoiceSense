# VoiceSense - Local Voice Cloning Desktop App

VoiceSense is an open-source, fully local voice cloning application. It allows you to clone your voice using a short 10-15 second reference audio and generate highly realistic speech from text. It runs entirely offline and utilizes the state-of-the-art **F5-TTS** flow-matching AI model.

The project consists of two main components:
1. **VoiceSense.UI**: A sleek, modern C# (WPF) desktop frontend for user interaction.
2. **VoiceSense.API**: A FastAPI-based Python backend that loads and runs the F5-TTS model using PyTorch and CUDA.

## 🚀 Features
- **100% Local & Offline**: No cloud dependencies or API keys required after the initial model download.
- **Ultra-Fast Generation**: Leverages NVIDIA GPUs (CUDA) for blazingly fast inference.
- **Reference Transcript Control**: Allows you to explicitly provide the transcript of your reference audio, bypassing Whisper to ensure perfect generation fluency.
- **Modern UI**: Simple and intuitive interface built with WPF.

---

## ⚙️ Prerequisites & System Requirements

To run VoiceSense, your system must meet the following requirements:
- **OS**: Windows 10 or 11
- **GPU**: NVIDIA GPU (highly recommended) for CUDA acceleration. It *can* run on CPU, but inference will take 5-10 minutes instead of 10 seconds.
- **Python**: Python 3.10 or 3.11 installed. *(Ensure "Add Python to PATH" is checked during installation).*
- **Visual Studio**: Visual Studio 2022 (to build/run the C# WPF project).

---

## 🛠️ Installation Guide

### Step 1: Install FFmpeg (Shared Build)
F5-TTS and Torchaudio *strictly require* the **shared** build of FFmpeg (which includes `.dll` files) on Windows.
Open a PowerShell terminal and run:
```bash
winget install -e --id Gyan.FFmpeg.Shared
```
*Note: Restart your terminal or PC after installation to ensure FFmpeg is added to your system PATH.*

### Step 2: Set Up the Python AI Backend
Open a terminal and navigate to the backend directory:
```bash
cd VoiceSense.API
```

Install the **GPU (CUDA) version** of PyTorch:
```bash
pip install torch torchvision torchaudio --index-url https://download.pytorch.org/whl/cu124
```

Install the remaining requirements:
```bash
pip install -r requirements.txt
```
*(Optional: If `f5-tts` throws errors regarding `torchcodec` missing DLLs, simply run `pip uninstall -y torchcodec` and torchaudio will safely fallback to the soundfile backend).*

### Step 3: Run the API Server
Start the FastAPI server by running:
```bash
python api.py
```
**First Run Notice**: On the very first run, the script will automatically download the F5-TTS model weights (~2.5 GB) from Hugging Face. Once it prints **"Yerel Yapay Zeka BAŞARIYLA yüklendi!"**, the server is ready. Leave this terminal open.

### Step 4: Run the C# Desktop App
1. Open `VoiceSense.sln` in Visual Studio.
2. Build and run the `VoiceSense.UI` project.

---

## 🎙️ Usage Instructions

For the best possible voice cloning quality, strictly follow these rules:

1. **Select Reference Audio**: Upload a pristine, 10-15 second `.wav` file of a single speaker in English. Ensure there is **no background noise**, music, or echo.
2. **Type the Reference Transcript**: In the left panel, type the *exact* words spoken in the reference audio. Missing a word or incorrect punctuation will severely degrade the output fluency.
3. **Type the Target Text**: In the right panel, type what you want the AI to say. **Use punctuation!** Commas (`,`) and periods (`.`) dictate where the AI will pause and breathe.
4. **Generate**: Click **"Sesi Üret"** (Generate Audio) and wait a few seconds. You can then play or save the generated `.wav` file.

## Troubleshooting
- **DLL Load Failed (`libtorchcodec`)**: Python 3.8+ changed how DLLs are loaded. The `api.py` script attempts to dynamically locate your FFmpeg installation using `shutil.which("ffmpeg")` and adds it via `os.add_dll_directory()`. Ensure you installed the *Shared* build of FFmpeg and that `ffmpeg` is accessible from your command line.
- **Timeout Exception**: The C# UI timeout is set to 60 minutes. If it times out, ensure the Python server is not throwing errors in the background terminal.
