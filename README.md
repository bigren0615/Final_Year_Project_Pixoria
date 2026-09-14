# Final Year Project - Artist E-Commerce System

Before developing, download everything below first

## Installation

1. Install latest node.js (https://nodejs.org/)

2. Install tailwind using cmd

```bash
  cd YOUR-PROJECT-PATH(same level with Program.cs)
  npm install -D tailwindcss @tailwindcss/cli
  npm install @tailwindcss/typography
```

3. Install daisy ui using cmd (in the same path)

```bash
  npm i -D daisyui@latest
```

4. Install Supabase using cmd (in the same path)

```bash
  dotnet add package Supabase
```

## Image Processing Setup (AI Tag Suggestions)

**Prerequisites:** Python 3.11.9

1. Download AI Models (First time only - ~2-3GB):
```bash
cd python_services
python verify_setup.py
```

2. Setup Python Virtual Environment:
```bash
cd python_services
python -m venv venv
Set-ExecutionPolicy -ExecutionPolicy RemoteSigned -Scope Process
.\venv\Scripts\activate
pip install -r requirements.txt
```

## Running the Application

**C# Backend:**

**Terminal - Python Image Service:**
```bash
Set-Location "C:\Users\foose\Desktop\Final_Year_Project\python_services"; &".\venv\Scripts\python.exe" image_tagger.py
```
Then run you project


## For Frontend

You can find UI icon at here: https://icons.getbootstrap.com/ & https://heroicons.com/ \
Just copy their svg code
