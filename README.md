# WordpressDrive

## About

WordpressDrive is a Windows Userspace Filesystem, that lets you access any Wordpress Site, that supports the Wordpress REST API (default) as a Windows Drive (Share).

Posts and Pages are organized in Folders as HTML Files. Media File types are represented by their file type depending from extension.

The Application starts as a taskbar icon, all user interaction can be started from there. Working with Wordpress data happens with the standard Windows tools (Explorer, CMD, ...).

Based on **WinFSP by Bill Zissimopoulos** [https://github.com/billziss-gh/winfsp](https://github.com/billziss-gh/winfsp)<br/>

## Use Cases
* If Wordpress content (probably mainly Media files) should be used on Windows it can directly opened from the Drive, or copied e.g. by drag&drop to other locations
* Content (Media files ) can easily added to wordpress by copy or drag&drop to the Drive
* If the Plugin is used on the site Media files can directly edited from the Drive without using the Wordpress UI
* Content can be edited with an arbitrary Editor without the modifications of the Wordpress Editor.
* ...

## Installation
Download the latest Release from [here](https://github.com/printpagestopdf/WordpressDrive/releases/latest).

Choose the appropriate one from the artifacts:  


**WordpressDriveSetup.exe:**  
Installer of WordpressDrive including WinFsp (best to use if not already installed WinFsp)

**WordpressDriveSetup.msi:**  
Installer of WordpressDrive (needs WinFsp installed)

**WordpressDriveFiles.zip:**  
Application Files without installer

Files ending with _x64 are usable for 64 bit Platforms only.
Files without the trailing _x64 can be used on 32 and 64 bit Plattforms.


## Usage
You can start WordpressDrive from the Startmenu (or put it into autostart).  

![Screenshot](assets/StartupMenu.png)  

On first startup (empty hostlist) the settings window is opened automatically, and new Wordpress host(s) can be registered.

![Screenshot](assets/SettingsFirst.png)  

Minimum host information is the Wordpress host url. If you don't want to login to the Wordpress site check "anonymous login". For you convinience give it a display name.
Closing the settings window (x cross at the top) will save this entry(s).

If you want to connect to your registered host you will find the WordpressDrive Icon in the Taskbar.  

![Screenshot](assets/WpdIcon.png)   
  
You open the popup Menu by using the right mouse button on the icon. Now you can select a host to connect to it (you will get a message if connection was successful).

If you want to work with the connected Wordpress host open e.g. the windows file explorer und you will find the drive connected:  
  
![Screenshot](assets/FileExplorer.png)   
  

## Wordpress Plugin
There is a Wordpress Plugin that enhances the capabilities of WordpressDrive.   
Even though WordpressDrive **works well without this plugin**, it extends its capabilities.

If installed on a Wordpress site WordpressDrive is able to use these additional features:

* getting (file) size information
* populating custom post types to WordpressDrive (REST API)
* **replacing/modifying media files**
* restrict media file modification by role and capability

You can install the plugin from the official Wordpress Plugin site:  
[https://wordpress.org/plugins/drive-wp/](https://wordpress.org/plugins/drive-wp/)

You will find the source code in this repo below the folder WpPlugin. 


