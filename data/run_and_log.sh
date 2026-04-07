#!/usr/bin/env bash
# Run the story downloader, clean first, and save output to run_output.log
# Usage: bash run_and_log.sh   (from the data/ directory)
set -e
SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
cd "$SCRIPT_DIR"

echo "Cleaning stories directory..."
rm -f stories/*.txt

echo "Running download_stories.py..."
python3 download_stories.py 2>&1 | tee run_output.log

echo ""
echo "Output saved to: $SCRIPT_DIR/run_output.log"
