import time
import os
from dotenv import load_dotenv
from google import genai
from google.genai import types

load_dotenv()

client = genai.Client(api_key=os.getenv("GOOGLE_API_KEY"))

prompt = """A high-quality, realistic, brightly lit video (16:9 aspect ratio) showcasing the intricate dexterity of a robotic arm in a modern kitchen setting.

### Composition and Scene
The scene is dominated by a clear, warm **wooden tabletop** located within what appears to be a domestic kitchen environment. The lighting is **high-key** and clear, simulating bright daylight or strong overhead kitchen illumination. Scattered across the wooden surface are several small, distinct objects: a black **pen**, a plastic **bottle**, several brightly colored **toy beverage cans/bottles**, a fresh orange **carrot**, and a yellow **resin banana**.

### Action and Motion
The primary subject is a sleek, industrial **robotic arm** that moves deliberately and smoothly into the center of the frame. The action is precisely paced, focusing on the arm's **manual dexterity**. The arm performs complex manipulations, sequentially selecting, grasping, and relocating the different-shaped items on the table. It handles the items with control, demonstrating its ability to interact with varied geometries, such as picking up the slim pen or shifting the bulkier toy beverages and food replicas.

### Camera Details
The egocentric camera maintains a **medium-close shot**, positioned slightly above and focused directly on the interaction zone—the robotic arm's end effector and the cluster of objects. The camera movement is **static or very smoothly tracking**, emphasizing the robotic arm's precise movements rather than the environment."""

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
generated_video.video.save("./videos/duplicate.mp4")
print("Generated video saved to ./videos/duplicate.mp4")