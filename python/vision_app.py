import os
import json
import requests
from pathlib import Path
from dotenv import load_dotenv

# Load environment variables
load_dotenv()
API_KEY = os.getenv("API_KEY")
BASE_URL = "https://vision-agent.api.reka.ai"

def display_menu():
    print("\n╔════════════════════════════════════════╗")
    print("║         Caption This - Main Menu       ║")
    print("╠════════════════════════════════════════╣")
    print("║ 1. List videos                         ║")
    print("║ 2. Caption a video by ID               ║")
    print("║ 3. Upload a video                      ║")
    print("║  ------------------------------------  ║")
    print("║ 4. List images                         ║")
    print("║ 5. Caption a image by URL              ║")
    print("║ 6. Upload a image                      ║")
    print("║  ------------------------------------  ║")
    print("║ 7. Delete a video by ID                ║")
    print("║  ------------------------------------  ║")
    print("║ x. Exit                                ║")
    print("╚════════════════════════════════════════╝")

def send_request(endpoint, body):
    headers = {
        "X-Api-Key": API_KEY,
        "Content-Type": "application/json"
    }
    response = requests.post(f"{BASE_URL}{endpoint}", json=body, headers=headers)
    return response

def save_to_file(content, filename):
    data_folder = Path(__file__).parent / "data"
    data_folder.mkdir(exist_ok=True)
    file_path = data_folder / filename
    with open(file_path, "w") as f:
        f.write(content)
    print(f"\n✅ Response saved to {file_path}\n")

def list_videos():
    print("\n📹 Getting all videos in library...\n")
    response = send_request("/videos/list", {"video_ids": []})
    data = response.json()
    
    if "results" in data:
        print(f"{'Video ID':<40} {'Video Name'}")
        print("-" * 80)
        for video in data["results"]:
            video_id = video.get("video_id", "N/A")
            video_name = video.get("metadata", {}).get("video_name", "N/A")
            print(f"{video_id:<40} {video_name}")
    else:
        print(json.dumps(data, indent=2))

def caption_video():
    print("\n🎬 Caption a video by ID\n")
    video_id = input("Enter video ID: ").strip()
    
    if not video_id:
        print("Video ID is required.")
        return
    
    request_body = {
        "video_id": video_id,
        "messages": [{
            "role": "user",
            "content": """write a text description that could be used to recreate this video as accurately as possible using an AI video generation model. Include details about: the video (aspect ratio, composition, style, motion, pacing, type of lighting, camera point of view, it's position related to the subject), objects descriptions (colors, location), and a description of what is happening and the interactions between objects in the video."""
            }]
    }
    
    response = send_request("/qa/chat", request_body)
    data = response.json()
    
    if "chat_response" in data:
        chat_json = json.loads(data["chat_response"])
        caption = ""
        if "sections" in chat_json:
            for section in chat_json["sections"]:
                if "markdown" in section:
                    caption += section["markdown"]
        save_to_file(caption, "video_captioned.json")
        print(caption)
    else:
        print(json.dumps(data, indent=2))

def upload_video():
    print("\n📤 Upload a video\n")
    print(f"Current folder: {os.getcwd()}\n")
    file_path = input("Enter video file path: ").strip()
    
    if not file_path or not os.path.exists(file_path):
        print("File not found.")
        return
    
    file_name = os.path.basename(file_path)
    
    with open(file_path, "rb") as f:
        files = {"file": (file_name, f, "video/mp4")}
        data = {
            "video_name": file_name,
            "index": "true"
        }
        headers = {"X-Api-Key": API_KEY}
        
        response = requests.post(f"{BASE_URL}/videos/upload", files=files, data=data, headers=headers)
        print(json.dumps(response.json(), indent=2))

def list_images():
    print("\n🖼️  Getting all images in library...\n")
    response = send_request("/images/list", {"image_ids": []})
    data = response.json()
    
    if "results" in data:
        count = len(data["results"])
        print(f"Found {count} image(s)\n")
        
        if count > 0:
            print(f"{'Image ID':<40} {'Image URL'}")
            print("-" * 120)
            for image in data["results"]:
                image_id = image.get("image_id", "N/A")
                image_url = image.get("image_url", "N/A")
                print(f"{image_id:<40} {image_url}")
    else:
        print(json.dumps(data, indent=2))

def caption_image():
    print("\n🖼️  Caption an image by URL\n")
    image_url = input("Enter image URL: ").strip()
    
    if not image_url:
        print("Image URL is required.")
        return
    
    request_body = {
        "messages": [{
            "role": "user",
            "content": [
                {
                    "type": "image_url",
                    "image_url": image_url
                },
                {
                    "type": "text",
                    "text": "Write a prompt, in plain text (no markdown), that would generate this exact image using an AI image generation model. Be detailed in your description, the subject, the colors, the lighting, the mood, and the style., the style of the image."
                }
            ]
        }],
        "model": "reka-flash"
    }
    
    headers = {
        "X-Api-Key": API_KEY,
        "Content-Type": "application/json"
    }
    
    response = requests.post("https://api.reka.ai/v1/chat/completions", json=request_body, headers=headers)
    data = response.json()
    
    if "choices" in data and len(data["choices"]) > 0:
        caption = data["choices"][0]["message"]["content"]
        save_to_file(caption, "image_captioned.json")
        print(caption)
    else:
        print(json.dumps(data, indent=2))

def upload_photo():
    print("\n🖼️  Upload a photo\n")
    print(f"Current folder: {os.getcwd()}\n")
    file_path = input("Enter photo file path: ").strip()
    
    if not file_path or not os.path.exists(file_path):
        print("File not found.")
        return
    
    file_name = os.path.basename(file_path)
    content_type = "image/png" if file_path.endswith(".png") else "image/jpeg"
    
    with open(file_path, "rb") as f:
        files = {"images": (file_name, f, content_type)}
        metadata = {
            "requests": [{
                "indexing_config": {"index": True},
                "metadata": {}
            }]
        }
        data = {"metadata": json.dumps(metadata)}
        headers = {"X-Api-Key": API_KEY}
        
        response = requests.post(f"{BASE_URL}/images/upload", files=files, data=data, headers=headers)
        print(json.dumps(response.json(), indent=2))

def delete_video():
    print("\n🗑️  Delete a video by ID\n")
    video_id = input("Enter video ID to delete: ").strip()
    
    if not video_id:
        print("Video ID is required.")
        return
    
    response = send_request("/videos/delete", {"video_ids": [video_id]})
    print(json.dumps(response.json(), indent=2))

def main():
    if not API_KEY:
        print("API Key is required. Exiting...")
        return
    
    running = True
    while running:
        display_menu()
        choice = input("\nSelect an option (1-7): ").strip()
        
        try:
            if choice == "1":
                list_videos()
            elif choice == "2":
                caption_video()
            elif choice == "3":
                upload_video()
            elif choice == "4":
                list_images()
            elif choice == "5":
                caption_image()
            elif choice == "6":
                upload_photo()
            elif choice == "7":
                delete_video()
            elif choice == "x":
                running = False
                print("\nGoodbye!")
            else:
                print("\nInvalid option. Please try again.")
        except Exception as e:
            print(f"❌ Error: {e}")
        
        if running:
            input("\nPress Enter to continue...")
            os.system('clear' if os.name == 'posix' else 'cls')

if __name__ == "__main__":
    main()
