# Final Year Project - Artist E-Commerce System

## About The Project

This project is a full-stack **Artist E-Commerce and Community Platform** developed as my Final Year Project.

The platform is designed to provide artists with a dedicated space to **showcase and sell their artworks**, while allowing users to discover artists, browse artwork collections, purchase artworks, and interact with the creative community.

The system combines e-commerce, community features, artwork management, and AI-assisted image processing to create an all-in-one platform for artists and art enthusiasts.

### Key Features

- 🎨 **Artwork Marketplace**  
  Artists can upload, manage, showcase, and sell their artworks.

- 👤 **Artist Profiles**  
  Dedicated artist profiles allow artists to showcase their work and provide information about themselves.

- 🛒 **E-Commerce System**  
  Users can browse artworks, add items to their cart, and place orders.

- 💳 **Order & Payment Flow**  
  Handles the purchasing process from cart checkout to order completion.

- 🖌️ **Artwork Commission**  
  Users can request custom artwork commissions from artists, while artists can manage commission requests and their commission workflow.

- 💬 **Community Features**  
  Provides features for users and artists to interact and engage with the creative community.

- 🏷️ **AI-Assisted Artwork Tagging**  
  Uses image processing and AI models to suggest relevant tags for uploaded artworks.

- 🔍 **Artwork Discovery**  
  Allows users to browse and discover artworks based on categories, tags, and other available information.

- 🔐 **User Authentication & Authorization**  
  Provides secure authentication and role-based access to different platform features.

### Technologies Used

**Backend**
- ASP.NET Core MVC 8
- C#
- Supabase

**Frontend**
- HTML / CSS
- Tailwind CSS
- daisyUI
- JavaScript

**AI / Image Processing**
- Python 3.11.9
- AI image tagging service

**Development Tools**
- Visual Studio
- Node.js
- Git / GitHub

---

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
Set-Location "C:\Users\User\Desktop\Final_Year_Project\python_services"; &".\venv\Scripts\python.exe" image_tagger.py
```
Then run you project

## Notes

- The Python image processing service must be running for AI-assisted artwork tagging to work.
- AI model downloads are only required during the initial setup.
- Do not commit local development configuration files or API keys to the repository.
- **The project requires the API keys and configuration values in `appsettings.Development.json` to run properly. If you need the required API keys or configuration for development, please contact me.**
