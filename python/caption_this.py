#!/usr/bin/env python3
"""
Caption This - Automated video captioning tool
Takes an MP4 file as input and generates descriptive captions using the Reka Vision API.
"""

import os
import sys
import time
import requests
import json
from pathlib import Path
from dotenv import load_dotenv

# Load environment variables
load_dotenv()
API_KEY = os.getenv("API_KEY")
BASE_URL = "https://vision-agent.api.reka.ai"

def print_step(step_num, message):
    """Print a formatted step message"""
    print(f"\n[Step {step_num}] {message}")

def print_status(message):
    """Print a status message"""
    print(f"  ✓ {message}")

def print_wait(message):
    """Print a waiting message"""
    print(f"  ⏳ {message}")

def upload_video(file_path):
    """Upload a video file and return the video_id"""
    print_step(1, "Uploading video...")
    
    if not os.path.exists(file_path):
        print(f"  ✗ Error: File not found: {file_path}")
        sys.exit(1)
    
    file_name = os.path.basename(file_path)
    print_status(f"Uploading {file_name}")
    
    headers = {
        "X-Api-Key": API_KEY
    }
    
    with open(file_path, 'rb') as f:
        files = {
            'file': (file_name, f, 'video/mp4'),
            'video_name': (None, file_name),
            'index': (None, 'true')
        }
        
        response = requests.post(
            f"{BASE_URL}/videos/upload",
            headers=headers,
            files=files
        )
    
    if response.status_code != 200:
        print(f"  ✗ Error uploading video: {response.status_code}")
        print(f"  Response: {response.text}")
        sys.exit(1)
    
    data = response.json()
    video_id = data.get("video_id")
    
    if not video_id:
        print(f"  ✗ Error: No video_id in response")
        print(f"  Response: {json.dumps(data, indent=2)}")
        sys.exit(1)
    
    print_status(f"Video uploaded successfully")
    print_status(f"Video ID: {video_id}")
    
    return video_id

def wait_for_indexing(video_id, max_attempts=60):
    """Poll the API until the video is indexed"""
    print_step(2, "Waiting for video indexing...")
    
    headers = {
        "X-Api-Key": API_KEY,
        "Content-Type": "application/json"
    }
    
    attempt = 0
    while attempt < max_attempts:
        response = requests.post(
            f"{BASE_URL}/videos/list",
            headers=headers,
            json={"video_ids": [video_id]}
        )
        
        if response.status_code != 200:
            print(f"  ✗ Error checking indexing status: {response.status_code}")
            sys.exit(1)
        
        data = response.json()
        
        if "results" in data and len(data["results"]) > 0:
            video = data["results"][0]
            indexing_status = video.get("indexing_status", "unknown")
            
            print_wait(f"Indexing status: {indexing_status} (attempt {attempt + 1}/{max_attempts})")
            
            if indexing_status == "indexed":
                print_status("Video indexing complete!")
                return True
        
        attempt += 1
        time.sleep(2)  # Wait 2 seconds before checking again
    
    print(f"  ✗ Error: Video indexing timed out after {max_attempts * 2} seconds")
    sys.exit(1)

def get_caption(video_id):
    """Get a caption for the video using qa/chat"""
    print_step(3, "Generating caption...")
    
    headers = {
        "X-Api-Key": API_KEY,
        "Content-Type": "application/json"
    }
    
    prompt = """Describe this video clearly and concisely in 2-3 sentences. 
    Include the main subject, key actions, setting, and visual style.
    Use plain text without markdown formatting."""
    
    request_body = {
        "video_id": video_id,
        "messages": [{
            "role": "user",
            "content": prompt
        }]
    }
    
    print_wait("Requesting caption from API...")
    
    response = requests.post(
        f"{BASE_URL}/qa/chat",
        headers=headers,
        json=request_body
    )
    
    if response.status_code != 200:
        print(f"  ✗ Error getting caption: {response.status_code}")
        print(f"  Response: {response.text}")
        sys.exit(1)
    
    data = response.json()
    
    # Extract the caption from the response
    caption = ""
    if "chat_response" in data:
        try:
            chat_json = json.loads(data["chat_response"])
            if "sections" in chat_json:
                for section in chat_json["sections"]:
                    if "markdown" in section:
                        caption += section["markdown"]
        except (json.JSONDecodeError, KeyError):
            caption = data.get("chat_response", "")
    
    if not caption:
        print(f"  ✗ Error: Could not extract caption from response")
        print(f"  Response: {json.dumps(data, indent=2)}")
        sys.exit(1)
    
    print_status("Caption generated successfully!")
    
    return caption

def save_caption(video_id, caption, file_path):
    """Save the caption to a JSON file"""
    print_step(4, "Saving caption...")
    
    data = {
        "video_id": video_id,
        "caption": caption,
        "timestamp": time.strftime("%Y-%m-%d %H:%M:%S")
    }
    
    output_dir = Path(file_path).parent / "captions"
    output_dir.mkdir(exist_ok=True)
    
    output_file = output_dir / f"{Path(file_path).stem}_caption.json"
    
    with open(output_file, 'w') as f:
        json.dump(data, f, indent=2)
    
    print_status(f"Caption saved to {output_file}")

def main():
    """Main function"""
    if len(sys.argv) < 2:
        print("Usage: python caption_this.py <video_file.mp4>")
        print("\nThis tool will:")
        print("  1. Upload the video to the Reka Vision API")
        print("  2. Wait for the video to be indexed")
        print("  3. Generate a descriptive caption")
        print("  4. Save the caption to a JSON file")
        sys.exit(1)
    
    video_file = sys.argv[1]
    
    print("\n" + "="*60)
    print("Caption This - Video Captioning Tool")
    print("="*60)
    
    # Step 1: Upload video
    video_id = upload_video(video_file)
    
    # Step 2: Wait for indexing
    wait_for_indexing(video_id)
    
    # Step 3: Get caption
    caption = get_caption(video_id)
    
    # Step 4: Save caption
    save_caption(video_id, caption, video_file)
    
    # Display the caption
    print("\n" + "="*60)
    print("Generated Caption:")
    print("="*60)
    print(caption)
    print("="*60 + "\n")

if __name__ == "__main__":
    main()
