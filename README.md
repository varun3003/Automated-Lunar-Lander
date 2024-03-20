# Automated Lunar Lander Using Reinforcement Learning

## Features
- Hazard detection
- Safe landing site selection
- Autonomous control for safe descent powered by Reinforcement Learning policy

## Installation steps
### Prerequisite software
- Unity: 2022.3.14f1
- Python 3.9.13

1. Clone repo
2. Create virtual environment using python 3.9.13
  ```
  py -3.9 -m venv venv
  ```
3. Activate virtual environment
  ```
  .\venv\Scripts\activate
  ```
3. Install dependencies
  - no GPU
  ```
  python -m pip install --upgrade pip
  pip install mlagents
  pip3 install torch torchvision torchaudio
  pip install protobuf==3.20.3
  pip install packaging
  ```
  - GPU (replace torch version cuxxx with the correct cuda version installed on your PC (eg.cu117), refer the url in the command to check availability)
  ```
  python -m pip install --upgrade pip
  pip install mlagents
  pip install torch==2.0.1+cuxxx -f https://download.pytorch.org/whl/torch_stable.html
  pip install protobuf==3.20.3
  pip install packaging
  ```
4. Add project to unity hub
5. Open project and select Demo Scene and press play to view the demo

