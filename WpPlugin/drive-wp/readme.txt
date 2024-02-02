=== Drive Wp ===
Contributors: theripper
Tags: windows drive, rest api, change attachment file
Requires at least: 4.7
Tested up to: 6.4.3
Requires PHP: 5.4
Stable tag: 0.5.1
License: GPLv3 or later
License URI: https://www.gnu.org/licenses/gpl-3.0.html

== Description ==
# Wp Drive

## The plugin
The Plugin enhances the capabilities of the Windows Application **"Wordpress Drive"**. If installed on a Wordpress site WordpressDrive is able to use these additional features:

* getting (file) size information
* populating custom post types to WordpressDrive (REST API)
* replacing/modifying media files <sup>1</sup>
* restrict media file modification by role and capability

<sup>1</sup> <sub>Most of the code for media replace is taken from the plugin "Easy Replace Image" [https://wordpress.org/plugins/easy-replace-image](https://wordpress.org/plugins/easy-replace-image) by [Iulia Cazan](https://profiles.wordpress.org/iulia-cazan/)

## The windows application

WordpressDrive is a Windows Userspace Filesystem, that lets you access any Wordpress Site, that supports the Wordpress REST API (default) as a Windows Drive (Share).

Posts and Pages are organized in Folders as HTML Files. Media File types are represented by their file type depending from extension.

The Application starts as a taskbar icon, all user interaction can be started from there. Working with Wordpress data happens with the standard Windows tools (Explorer, CMD, ...).

You can download the installation Files of **WordpressDrive** from here:
[https://github.com/printpagestopdf/WordpressDrive/releases/latest](https://github.com/printpagestopdf/WordpressDrive/releases/latest)  
  
Find the sourcecode here:  
[https://github.com/printpagestopdf/WordpressDrive](https://github.com/printpagestopdf/WordpressDrive)  

= Demo =
None

== Installation ==
* Upload `Wp Drive` to the `/wp-content/plugins/` directory of your application
* Login as Admin
* Activate the plugin through the 'Plugins' menu in WordPress

== Screenshots ==
1. Admin Settings for the plugin.
2. Windows Explorer view of a Wordpress site.

== Frequently Asked Questions ==
None

== Changelog ==
= 0.5.1 =
* Bugfix admin screen if there are none custom posttypes
* tested with wp 6.4.3

= 0.5 =
* Allow to populate custom post types to REST API
* Allow to replace file with a new uploaded image via REST API
* Allow to restrict media file replace by role and capability
* Add (file) size information to REST API

== Upgrade Notice ==
None

== License ==
This program is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.

== Version history ==
0.5.1 - Bugfix release
0.5 - Initial release.
