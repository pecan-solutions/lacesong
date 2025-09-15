#!/bin/bash

# GitHub Pages setup script for Lacesong mod index
# This script sets up GitHub Pages hosting for the official mod index

set -e

echo "Setting up GitHub Pages for Lacesong mod index..."

# create docs directory if it doesn't exist
mkdir -p docs

# create index.html for GitHub Pages
cat > docs/index.html << 'EOF'
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>Lacesong Mod Index</title>
    <style>
        body {
            font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif;
            max-width: 800px;
            margin: 0 auto;
            padding: 20px;
            line-height: 1.6;
            color: #333;
        }
        .header {
            text-align: center;
            margin-bottom: 40px;
            padding: 20px;
            background: #f8f9fa;
            border-radius: 8px;
        }
        .header h1 {
            margin: 0;
            color: #2c3e50;
        }
        .header p {
            margin: 10px 0 0 0;
            color: #666;
        }
        .info-box {
            background: #e8f4fd;
            border: 1px solid #bee5eb;
            border-radius: 6px;
            padding: 15px;
            margin: 20px 0;
        }
        .info-box h3 {
            margin: 0 0 10px 0;
            color: #0c5460;
        }
        .endpoint {
            background: #f8f9fa;
            border: 1px solid #dee2e6;
            border-radius: 4px;
            padding: 10px;
            margin: 10px 0;
            font-family: 'Monaco', 'Consolas', monospace;
        }
        .endpoint strong {
            color: #495057;
        }
        .footer {
            text-align: center;
            margin-top: 40px;
            padding: 20px;
            color: #666;
            border-top: 1px solid #eee;
        }
    </style>
</head>
<body>
    <div class="header">
        <h1>Lacesong Mod Index</h1>
        <p>Official mod index for Hollow Knight: Silksong</p>
    </div>

    <div class="info-box">
        <h3>About This Index</h3>
        <p>This is the official mod index for Lacesong, a cross-platform mod management tool for Hollow Knight: Silksong. 
        The index contains curated mods from trusted sources and community repositories.</p>
    </div>

    <h2>API Endpoints</h2>
    
    <div class="endpoint">
        <strong>GET</strong> <code>/mods.json</code><br>
        Returns the complete mod index with all available mods and metadata.
    </div>

    <div class="endpoint">
        <strong>GET</strong> <code>/mods.json?category=UI</code><br>
        Returns mods filtered by category (General, UI, Gameplay, Graphics, Audio, Utility, Developer).
    </div>

    <div class="endpoint">
        <strong>GET</strong> <code>/mods.json?official=true</code><br>
        Returns only official/verified mods.
    </div>

    <h2>Mod Index Schema</h2>
    <p>The mod index follows a structured JSON schema with the following key components:</p>
    <ul>
        <li><strong>Mods:</strong> Array of mod entries with metadata, versions, and download URLs</li>
        <li><strong>Categories:</strong> Available mod categories for filtering</li>
        <li><strong>Repositories:</strong> Source repositories for mod discovery</li>
        <li><strong>Versioning:</strong> Support for multiple versions per mod with changelogs</li>
        <li><strong>Dependencies:</strong> Mod dependency resolution</li>
        <li><strong>Verification:</strong> Official and verified mod status</li>
    </ul>

    <h2>Integration</h2>
    <p>To integrate with this mod index in your application:</p>
    <ol>
        <li>Fetch the mod index from <code>https://lacesong.dev/mods.json</code></li>
        <li>Parse the JSON response to get available mods</li>
        <li>Use the download URLs to install mods</li>
        <li>Check for updates by comparing version numbers</li>
    </ol>

    <div class="info-box">
        <h3>Adding Custom Repositories</h3>
        <p>Users can add their own mod repositories using the Lacesong application. 
        Custom repositories must follow the same JSON schema and be accessible via HTTP/HTTPS.</p>
    </div>

    <div class="footer">
        <p>Lacesong Mod Index • <a href="https://github.com/wwdarrenwei/lacesong">GitHub Repository</a></p>
        <p>Last updated: <span id="lastUpdated"></span></p>
    </div>

    <script>
        // Update last updated timestamp
        fetch('mods.json')
            .then(response => response.json())
            .then(data => {
                const lastUpdated = new Date(data.lastUpdated).toLocaleDateString();
                document.getElementById('lastUpdated').textContent = lastUpdated;
            })
            .catch(error => {
                document.getElementById('lastUpdated').textContent = 'Unknown';
            });
    </script>
</body>
</html>
EOF

# create .nojekyll file to disable Jekyll processing
touch docs/.nojekyll

# create a simple README for the docs directory
cat > docs/README.md << 'EOF'
# Lacesong Mod Index Documentation

This directory contains the GitHub Pages hosting for the Lacesong mod index.

## Files

- `index.html` - Main landing page with API documentation
- `mods.json` - The actual mod index data
- `.nojekyll` - Disables Jekyll processing for GitHub Pages

## Setup

1. Enable GitHub Pages in your repository settings
2. Set source to "Deploy from a branch"
3. Select the `main` branch and `/docs` folder
4. The mod index will be available at `https://yourusername.github.io/lacesong/`

## Updating the Index

To update the mod index:

1. Run the admin script: `dotnet script tools/admin.csx build`
2. Copy the generated `mods.json` to this directory
3. Commit and push changes
4. GitHub Pages will automatically update

## Custom Domain

To use a custom domain (e.g., lacesong.dev):

1. Add a `CNAME` file with your domain name
2. Configure DNS to point to GitHub Pages
3. Enable HTTPS in GitHub Pages settings
EOF

echo "GitHub Pages setup complete!"
echo ""
echo "Next steps:"
echo "1. Enable GitHub Pages in your repository settings"
echo "2. Set source to 'Deploy from a branch' -> 'main' -> '/docs'"
echo "3. Run 'dotnet script tools/admin.csx build' to generate the mod index"
echo "4. Copy the generated mods.json to the docs/ directory"
echo "5. Commit and push changes"
echo ""
echo "Your mod index will be available at: https://yourusername.github.io/lacesong/"
