import time
import os
from dotenv import load_dotenv
from google import genai
from google.genai import types

load_dotenv()

client = genai.Client(api_key=os.getenv("GOOGLE_API_KEY"))

prompt = """____HERE_GOES_THE_PROMPT____"""

operation = client.models.generate_videos(
    model="veo-3.1-generate-preview",
    prompt=prompt,
)

# Poll the operation status until the video is ready.
while not operation.done:
    print("Waiting for video generation to complete...")
    time.sleep(10)
    operation = client.operations.get(operation)

# Download the generated video.
generated_video = operation.response.generated_videos[0]
client.files.download(file=generated_video.video)
now_str = time.strftime("%Y-%m-%d-%Hh%M")
output_path = f"./videos/duplicate_{now_str}.mp4"
generated_video.video.save(output_path)
print(f"Generated video saved to {output_path}")