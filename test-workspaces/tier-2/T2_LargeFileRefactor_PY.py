import os
import time
import random
import datetime
import shutil

# --- Configuration & Constants ---
SUPPORTED_EXTENSIONS = ('.mp4', '.mkv', '.avi', '.mov', '.wmv')
MAX_RETRIES = 3
THUMBNAIL_PREFIX = "thumb_"
TEMP_DIR = "./temp_proc"

class MediaError(Exception):
    """Custom exception for media processing errors."""
    pass

class MediaFile:
    """Represents a single media asset and its properties."""
    def __init__(self, path):
        self.path = path
        self.name = os.path.basename(path)
        self.extension = os.path.splitext(path)[1].lower()
        self.size_bytes = os.path.getsize(path)
        self.metadata = {}
        self.thumbnail_path = None
        self.status = "pending"

    def __str__(self):
        return f"{self.name} ({self.size_bytes / 1024:.1f} KB)"

class LocalMediaProcessor:
    """Main engine for scanning, processing, and cataloging media files."""
    def __init__(self, root_dir):
        self.root_dir = root_dir
        self.database = []
        self.session_id = random.randint(1000, 9999)
        print(f"Initialized LocalMediaProcessor [Session: {self.session_id}]")
        print(f"Target Directory: {root_dir}")

    def validate_workspace(self):
        print("Validating workspace...")
        if not os.path.exists(self.root_dir):
            print(f"CRITICAL: Root directory '{self.root_dir}' does not exist.")
            return False
        
        if not os.path.exists(TEMP_DIR):
            print(f"Creating temporary directory: {TEMP_DIR}")
            os.makedirs(TEMP_DIR)
        
        print("Workspace validation successful.")
        return True

    def scan_directory(self):
        print(f"Scanning {self.root_dir} for supported media types...")
        found_files = []
        
        try:
            for root, dirs, files in os.walk(self.root_dir):
                for file in files:
                    if file.lower().endswith(SUPPORTED_EXTENSIONS):
                        full_path = os.path.join(root, file)
                        media_item = MediaFile(full_path)
                        found_files.append(media_item)
                        print(f" -> Found: {file}")
            
            print(f"Scan complete. Found {len(found_files)} files.")
        except Exception as e:
            print(f"Error during directory walk: {str(e)}")
            
        return found_files

    def extract_metadata(self, media_file):
        print(f"[{media_file.name}] Extracting technical metadata...")
        time.sleep(0.1) # Simulate IO delay
        
        # Simulate an intermittent technical failure
        if random.random() < 0.05:
            print(f"[{media_file.name}] FAILED: Header corrupted or inaccessible.")
            raise Exception("Metadata extraction failed")

        media_file.metadata = {
            "duration": random.randint(30, 3600),
            "codec": random.choice(["h264", "hevc", "vp9", "av1"]),
            "bitrate": random.randint(1500, 15000),
            "resolution": random.choice(["1080p", "4K", "720p"]),
            "extracted_at": datetime.datetime.now().strftime("%Y-%m-%d %H:%M:%S")
        }
        print(f"[{media_file.name}] Metadata: {media_file.metadata['codec']} @ {media_file.metadata['resolution']}")

    def generate_thumbnail(self, media_file):
        print(f"[{media_file.name}] Generating preview thumbnail...")
        
        if media_file.size_bytes > 1024 * 1024 * 100:
            print(f"[{media_file.name}] Note: High bitrate file, processing may take longer.")
            time.sleep(0.3)
        else:
            time.sleep(0.1)

        # Simulate GPU/Resource failure
        if random.random() < 0.03:
            print(f"[{media_file.name}] FAILED: Frame buffer allocation error.")
            raise Exception("Thumbnail rendering failed")

        thumb_name = THUMBNAIL_PREFIX + media_file.name + ".png"
        media_file.thumbnail_path = os.path.join(TEMP_DIR, thumb_name)
        
        # Mock file creation
        with open(media_file.thumbnail_path, 'w') as f:
            f.write("MOCK_THUMBNAIL_DATA")
            
        print(f"[{media_file.name}] Thumbnail saved: {media_file.thumbnail_path}")

    def sync_to_internal_db(self, media_file):
        print(f"[{media_file.name}] Finalizing database entry...")
        time.sleep(0.05)
        
        record = {
            "name": media_file.name,
            "path": media_file.path,
            "size": media_file.size_bytes,
            "metadata": media_file.metadata,
            "thumb": media_file.thumbnail_path,
            "status": "archived"
        }
        self.database.append(record)
        media_file.status = "complete"
        print(f"[{media_file.name}] Record committed to registry.")

    def run_pipeline(self):
        print("\n" + "="*40)
        print("MEDIA PROCESSING PIPELINE START")
        print("="*40)
        
        if not self.validate_workspace():
            return

        all_files = self.scan_directory()
        if not all_files:
            print("Nothing to process. Pipeline terminated.")
            return

        processed = 0
        skipped = 0

        for m_file in all_files:
            print(f"\n--- Processing Item: {m_file.name} ---")
            try:
                self.extract_metadata(m_file)
                self.generate_thumbnail(m_file)
                self.sync_to_internal_db(m_file)
                processed += 1
            except Exception as error:
                print(f"CRITICAL FAILURE on {m_file.name}: {error}")
                m_file.status = "failed"
                skipped += 1
        
        print("\n" + "="*40)
        print("PIPELINE SUMMARY")
        print(f"Successfully Processed: {processed}")
        print(f"Failed/Skipped:        {skipped}")
        print("="*40 + "\n")

    def print_final_report(self):
        print("\n" + "#"*40)
        print("GENERATING FINAL CATALOG REPORT")
        print("#"*40)
        
        if not self.database:
            print("No data available in catalog.")
            return

        total_storage = sum(item["size"] for item in self.database)
        resolutions = {}
        
        for item in self.database:
            res = item["metadata"].get("resolution", "unknown")
            resolutions[res] = resolutions.get(res, 0) + 1
        
        print(f"Total Catalog Size: {total_storage / (1024*1024):.2f} MB")
        print("Resolution Breakdown:")
        for r_name, r_count in resolutions.items():
            print(f"  - {r_name}: {r_count}")
        print("#"*40 + "\n")

# --- Utility Functions ---

def setup_mock_environment():
    print("Setting up mock environment for testing...")
    mock_path = "./mock_assets"
    if os.path.exists(mock_path):
        shutil.rmtree(mock_path)
    
    os.makedirs(mock_path)
    dummy_files = ["intro.mp4", "vacation_2023.mkv", "tutorial.avi", "corrupt_data.mov", "outro.mp4"]
    
    for df in dummy_files:
        p = os.path.join(mock_path, df)
        with open(p, 'w') as f:
            f.write("dummy content" * 100)
    
    print(f"Mock environment created at {mock_path}")
    return mock_path

def cleanup_workspace():
    print("Initiating global cleanup...")
    if os.path.exists(TEMP_DIR):
        print(f"Removing temporary directory: {TEMP_DIR}")
        shutil.rmtree(TEMP_DIR)
    print("Cleanup sequence finished.")

def system_preflight_check():
    print("Running system preflight checks...")
    checks = ["disk_space", "io_permissions", "codec_availability"]
    for c in checks:
        print(f"Checking {c}...")
        time.sleep(0.05)
    print("Preflight checks complete. System ready.")

# --- Entry Point ---

if __name__ == "__main__":
    # Standard flow
    system_preflight_check()
    
    assets_dir = setup_mock_environment()
    
    try:
        processor = LocalMediaProcessor(assets_dir)
        processor.run_pipeline()
        processor.print_final_report()
    except Exception as fatal_e:
        print(f"FATAL SYSTEM ERROR: {fatal_e}")
    finally:
        cleanup_workspace()
        print("Program exiting.")
