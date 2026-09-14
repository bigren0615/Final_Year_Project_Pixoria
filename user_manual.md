# User Manual

## Appendix A: User Guide

### System Document

#### Hardware and Software Requirements

To successfully install and run the **Pixoria** application, the following hardware and software specifications are required:

**Hardware Requirements:**
*   **Processor:** Modern Multi-core Processor (Intel i5/Ryzen 5 or better recommended for AI services).
*   **RAM:** Minimum 8GB (16GB recommended for running local AI models).
*   **Storage:** At least 5GB of free space (approx. 2GB for AI models).
*   **Internet Connection:** Required for connecting to Supabase, Stripe, and Gemini APIs.

**Software Requirements:**
*   **Operating System:** Windows 10/11, macOS, or Linux.
*   **Runtime Environments:**
    *   [.NET 8.0 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/8.0)
    *   [Python 3.11.9](https://www.python.org/downloads/)
    *   [Node.js](https://nodejs.org/) (for Tailwind CSS compilation)
*   **Database:** Supabase (Cloud-hosted).

#### Live Deployment

The application is currently hosted on Amazon Web Services (AWS) and is accessible online.

*   **Live Website:** [https://pixoria.bigren.me/](https://pixoria.bigren.me/)
*   **Hosting Provider:** AWS

> **Note:** The hosting is provided via an AWS Learner Lab account. The server has a session limit of **4 hours** per startup. If the website is inaccessible, the lab environment may need to be manually restarted.
>
> **Credentials:** Due to password protection policies, the AWS account username and password have been provided in a separate file named `AWS Account.txt`.

#### Installation Guide

Follow these steps to set up the application on your local machine.

**Step 1: Clone/Extract the Project**
Ensure the project files are extracted to a local directory (e.g., `C:\Users\User\Desktop\Final_Year_Project`).

**Step 2: Configure Environment**
1.  Navigate to `Final_Year_Project/appsettings.json`.
2.  Ensure the following API keys and configurations are set (refer to the provided `appsettings.json` for structure):
    *   **Supabase**: `Url` and `AnonKey`.
    *   **Stripe**: `SecretKey` and `PublishableKey`.
    *   **Gemini**: `ApiKey`.
    *   **Smtp**: Email credentials for sending notifications.

**Step 3: Setup Python AI Services**
The application uses a local Python service for image tagging and fraud detection.

1.  Open a terminal (PowerShell) and navigate to the `python_services` folder:
    ```powershell
    cd python_services
    ```
2.  Run the setup script to create a virtual environment and install dependencies:
    ```powershell
    .\setup.ps1
    ```
    *Alternatively, manually run:*
    ```powershell
    python -m venv venv
    .\venv\Scripts\Activate.ps1
    pip install -r requirements.txt
    ```
3.  Download the required AI models (WD14 and CLIP-L):
    ```powershell
    python startup.py --download-models
    ```
    *(Note: This may take 15-30 minutes depending on internet speed).*

**Step 4: Setup .NET Application**
1.  Open a new terminal and navigate to the `Final_Year_Project` folder (where the `.csproj` file is located).
2.  Install frontend dependencies:
    ```powershell
    npm install
    ```
3.  Build the CSS styles:
    ```powershell
    npm run css:build
    ```
4.  Restore .NET packages:
    ```powershell
    dotnet restore
    ```

---

### Operation Document

#### Getting Started

**1. Start the Python AI Service**
Before running the main application, the Python service must be running.
1.  Navigate to `python_services`.
2.  Run the startup script:
    ```powershell
    .\quick_start.ps1
    ```
    *Or manually:*
    ```powershell
    .\venv\Scripts\Activate.ps1
    python startup.py
    ```
    *Ensure the service is running on `http://127.0.0.1:5000`.*

**2. Start the Web Application**
1.  Navigate to the `Final_Year_Project` folder.
2.  Run the application:
    ```powershell
    dotnet run
    ```
3.  Open your web browser and navigate to `http://localhost:5000` (or the port shown in the terminal).

#### User Roles and Login

The system supports three primary user roles:
*   **Customer**: Can browse artwork, follow artists, and make purchases.
*   **Artist**: Can upload artwork, manage commissions, and view sales analytics.
*   **Admin**: Has access to fraud detection monitoring and system management.

**Login / Registration**
*   **New Users**: Click "Register" on the top navigation bar. Fill in your details (Name, Username, Email, Password).
    *   [Screenshot needed: Registration Page]
*   **Existing Users**: Click "Login" and enter your credentials.
    *   [Screenshot needed: Login Page]

#### Features Guide

**1. Home Page (Customer/Artist)**
Upon logging in, users are greeted with the home page displaying the latest and followed artworks.
*   **Navigation Bar**: Access Profile, Cart, and Settings.
*   **Feed**: Scroll to view recent posts.
    *   [Screenshot needed: Home Page Feed]

**2. Artist Features**
*   **Upload Artwork**: Navigate to the "Post" section to upload new art. The system will automatically tag your image using the AI service.
    *   [Screenshot needed: Artwork Upload Page with AI Tags]
*   **Manage Commissions**: View and respond to commission requests from customers.

**3. Admin Features**
*   **Fraud Detection Dashboard**: Admins are automatically redirected to the Fraud Detection Dashboard upon login.
*   **Scan & Monitor**: View flagged artworks and potential plagiarism cases detected by the AI.
    *   [Screenshot needed: Fraud Detection Dashboard]
*   **Subscription Management**: Manually expire subscriptions if necessary via the admin panel.

---

## Appendix B: Developer Guide

### Software and Libraries

**Backend (.NET 8.0)**
*   **Supabase**: Database client and authentication.
*   **Stripe.net**: Payment processing.
*   **Google_GenerativeAI**: Integration with Gemini AI.
*   **HtmlSanitizer**: Security for user inputs.

**AI Services (Python 3.11)**
*   **Flask**: REST API framework.
*   **Torch & Torchvision**: Deep learning framework.
*   **Transformers**: Hugging Face models for CLIP.
*   **OnnxRuntime**: Efficient inference for WD14 tagger.
*   **Pillow**: Image processing.

**Frontend**
*   **Tailwind CSS**: Utility-first CSS framework.
*   **Razor Views**: Server-side rendering.

### API Reference
*   **Python Service API**: Running on `localhost:5000`.
    *   `POST /tag`: Accepts an image and returns WD14 tags.
    *   `POST /detect-fraud`: Analyzes image for potential fraud/plagiarism.
