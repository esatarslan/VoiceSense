import os
import shutil
import traceback
from fastapi import FastAPI, UploadFile, File, Form, HTTPException
from fastapi.responses import FileResponse

# FFmpeg yolunu dinamik olarak bul ve DLL'leri yükle (Python 3.8+ Windows için)
ffmpeg_exe = shutil.which("ffmpeg")
if ffmpeg_exe:
    FFMPEG_PATH = os.path.dirname(ffmpeg_exe)
    if hasattr(os, "add_dll_directory"):
        try:
            os.add_dll_directory(FFMPEG_PATH)
        except Exception as e:
            print(f"FFmpeg DLL klasoru eklenemedi: {e}")
else:
    print("UYARI: Sistem PATH uzerinde 'ffmpeg' bulunamadi. Torchaudio hata verebilir.")

app = FastAPI()

is_model_loaded = False
ema_model = None
vocoder = None
vocoder_name = "vocos"

def init_models():
    global is_model_loaded, ema_model, vocoder
    if is_model_loaded:
        return
    try:
        print("Yerel Yapay Zeka (F5-TTS) yükleniyor, lütfen bekleyin...")
        import f5_tts
        from f5_tts.infer.utils_infer import load_model, load_vocoder, device
        from hydra.utils import get_class
        from omegaconf import OmegaConf
        from cached_path import cached_path
        from importlib.resources import files

        vocoder = load_vocoder(vocoder_name=vocoder_name, is_local=False, device=device)
        
        model_cfg_path = str(files("f5_tts").joinpath("configs/F5TTS_Base.yaml"))
        model_cfg = OmegaConf.load(model_cfg_path)
        model_cls = get_class(f"f5_tts.model.{model_cfg.model.backbone}")
        
        ckpt_file = str(cached_path("hf://SWivid/F5-TTS/F5TTS_Base/model_1200000.safetensors"))
        vocab_file = str(cached_path("hf://SWivid/F5-TTS/F5TTS_Base/vocab.txt"))
        
        ema_model = load_model(
            model_cls, model_cfg.model.arch, ckpt_file, mel_spec_type=vocoder_name, vocab_file=vocab_file, device=device
        )
        print("Yerel Yapay Zeka BAŞARIYLA yüklendi!")
        is_model_loaded = True
    except Exception as e:
        print("Yapay zeka modelleri yüklenirken hata oluştu:")
        traceback.print_exc()

@app.on_event("startup")
async def startup_event():
    init_models()

@app.post("/clone-voice")
async def clone_voice(
    text: str = Form(...),
    ref_text: str = Form(""),
    audio_file: UploadFile = File(...)
):
    if not is_model_loaded:
        raise HTTPException(status_code=500, detail="Yerel modeller yüklenemedi. Sunucu ekranındaki hatayı kontrol edin.")
        
    temp_ref_path = f"temp_ref_{audio_file.filename}"
    output_dir = "output_audio"
    os.makedirs(output_dir, exist_ok=True)
    
    with open(temp_ref_path, "wb") as buffer:
        shutil.copyfileobj(audio_file.file, buffer)

    try:
        from f5_tts.infer.utils_infer import (
            infer_process, preprocess_ref_audio_text,
            target_rms, cross_fade_duration, nfe_step, cfg_strength, sway_sampling_coef, speed, fix_duration, device
        )
        import soundfile as sf
        import numpy as np
        
        if ref_text.strip():
            print(f"Kullanici tarafindan saglanan referans metni kullaniliyor: '{ref_text}'")
            ref_audio_path = temp_ref_path
            final_ref_text = ref_text
        else:
            print(f"Ses isleniyor. Whisper ile referans dinleniyor...")
            ref_audio_path, final_ref_text = preprocess_ref_audio_text(temp_ref_path, "")
            print(f"Whisper referansı anladı: '{final_ref_text}'.")
        
        import re as _re
        import torchaudio as _ta
        from f5_tts.infer.utils_infer import infer_batch_process

        # Sadece cümle sonlarından böl (.!?) → virgülde bölünme olmaz
        sentences = [s.strip() for s in _re.split(r'(?<=[.!?])\s+', text.strip()) if s.strip()]
        if not sentences:
            sentences = [text.strip()]
        print(f"Sesi üretiyor ({len(sentences)} cümle): {sentences}")

        ref_audio_tensor, ref_sr = _ta.load(ref_audio_path)

        audio_segment, final_sample_rate, _ = next(infer_batch_process(
            (ref_audio_tensor, ref_sr),
            final_ref_text,
            sentences,
            ema_model,
            vocoder,
            mel_spec_type=vocoder_name,
            target_rms=target_rms,
            cross_fade_duration=cross_fade_duration,
            nfe_step=nfe_step,
            cfg_strength=cfg_strength,
            sway_sampling_coef=sway_sampling_coef,
            speed=speed,
            fix_duration=fix_duration,
            device=device
        ))
        
        from datetime import datetime
        timestamp = datetime.now().strftime("%Y%m%d_%H%M%S")
        filename = f"generated_{timestamp}.wav"
        output_file_path = os.path.join(output_dir, filename)
        sf.write(output_file_path, audio_segment, final_sample_rate)
        
        return FileResponse(output_file_path, media_type="audio/wav", filename=filename)
        
    except Exception as e:
        print("Ses üretilirken hata:")
        traceback.print_exc()
        raise HTTPException(status_code=500, detail=str(e))
    finally:
        if os.path.exists(temp_ref_path):
            os.remove(temp_ref_path)

if __name__ == "__main__":
    import uvicorn
    print("Yerel F5-TTS API Sunucusu Baslatiliyor... http://127.0.0.1:8000")
    uvicorn.run(app, host="127.0.0.1", port=8000)
