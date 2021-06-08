<?php
/**
 * Plugin Name: Drive Wp
 * Plugin URI: https://github.com/printpagestopdf/WordpressDrive
 * Description: Enhances the capabilities of the Windows Application "WordpressDrive" by adding content size Information, ability to modify media files and to populate custom post types to REST API. 
 * Text Domain: wp-drive
 * Domain Path: /languages/
 * Version: 0.5.0
 * Author: The Ripper
 * Author URI: https://profiles.wordpress.org/theripper
 * Requires at least: 4.7
 * Tested up to: 5.7
 * Requires PHP: 5.4
 * License: GPLv3 or later
 *
 * @package wp-drive
 *
 * Copyright (C) 2021 The Ripper
 *
 * This program is free software; you can redistribute it and/or modify
 * it under the terms of the GNU General Public License, version 3, as
 * published by the Free Software Foundation.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program; if not, write to the Free Software
 * Foundation, Inc., 51 Franklin St, Fifth Floor, Boston, MA  02110-1301  USA
 */


// If this file is called directly, abort.
if ( ! defined( 'WPINC' ) ) {
	die;
}

// Define the plugin version.
define( 'WPDRIVE_PLUGIN_VERSION', 0.5 );

/**
 * Class to extend the wp API for wordpress drive
 */
class Wpdrive_api_extension extends WP_REST_Controller{

	const PLUGIN_NAME        = 'Drive Wp';
	const PLUGIN_SUPPORT_URL = 'https://wordpress.org/support/plugin/wp-drive/';
	const PLUGIN_ID   = 'wp-drive';
	
	const MOD_MEDIA_ROLE= "modifymedia";
	const MOD_MEDIA_CAP = "modify_media";
	
	private $check_modmedia_cap=false;
	private $api_visible_types=array();
	

	/**
	 * Class instance.
	 *
	 * @var object
	 */
	private static $instance;

	/**
	 * Get active object instance
	 *
	 * @return object
	 */
	public static function get_instance() {
		if ( ! self::$instance ) {
			self::$instance = new Wpdrive_api_extension();
		}		
		return self::$instance;
	}

	/**
	 * Class constructor
	 *
	 * @return void
	 */
	public function __construct() {
						
		$wordpress_drive_options = get_option( 'wordpress_drive_option_name' );
		$this->check_modmedia_cap =isset( $wordpress_drive_options['use_capability_cb']);
		
		if(!empty(@$wordpress_drive_options['post_types_select']))
		{
			$this->api_visible_types=$wordpress_drive_options['post_types_select'];
			add_filter( 'register_post_type_args', array( $this,'post_type_api_visibility'), 10, 2 );
		}
		
		add_action( 'rest_api_init',array( $this,'on_api_init'));
		add_action( 'plugins_loaded', array( get_called_class(), 'plugin_ver_check' ) );
		
		// add_filter( 'rest_post_dispatch', array($this,'log_rest_api_errors'), 10, 3 );	
		
		
		
		if ( is_admin() )
		{
			add_action( 'init', array($this,'wp_drive_load_textdomain') ); 
			require_once("includes/admin-ui.php");
			$WpDriveUI = new WpDriveUI(self::MOD_MEDIA_ROLE,self::MOD_MEDIA_CAP);
		}
		
	}
	
	function wp_drive_load_textdomain() {
		load_plugin_textdomain( 'wp-drive', false, basename( dirname( __FILE__ ) ) . '/languages/' );
	}	
	
	function post_type_api_visibility( $args, $post_name )
	{
		if (!in_array($post_name,$this->api_visible_types) )
			return $args;

		$args['show_in_rest'] = true;
		return $args;
	}		
	
	public function on_api_init() 
	{
		$this->register_routes();
		$this->register_rest_fields();
		
	}
		
	/**
	 * Log REST API errors
	 *
	 * @param WP_REST_Response $result  Result that will be sent to the client.
	 * @param WP_REST_Server   $server  The API server instance.
	 * @param WP_REST_Request  $request The request used to generate the response.
	 */
	public function log_rest_api_errors( $result, $server, $request ) {
		if ( !$result->is_error() ) return $result; 
		
		error_log( sprintf(
			"REST request: %s: %s",
			$request->get_route(),
			print_r( $request->get_params(), true )
		) );

		error_log( sprintf(
			"REST result: %s: %s",
			$result->get_matched_route(),
			print_r( $result->get_data(), true )
		) );

		return $result;
	}


/************************* Begin api Controller **********************************************/	
	public function register_rest_fields()
	{
		register_rest_field( array('post','page','attachment'), 'file_size', array(
			   'get_callback'    => array($this,'get_file_size'),
			   'schema'          => null,
			)
		);
		
	}


	public function get_file_size($object)
	{
		switch($object['type'])
		{
			case "page":
			case "post":
				if(null !== @$object['content']['raw'])
					$file_size=mb_strlen($object['content']['raw'],'8bit');
				else
					$file_size=mb_strlen(get_the_content($object),'8bit');
				break;
			case "attachment":
				$att_path=self::get_attachment_path($object['id']);
				if(!empty($att_path) && file_exists($att_path)) {
					$file_size=filesize($att_path);
				}
				else
					$file_size=1024;
				break;
			default:
				$file_size=1024;
				break;
			
		}
		
		return $file_size;
	}

	public function register_routes() {
		$version = '1';
		$namespace = 'wp-drive/v' . $version;
		$base = 'attachment';
		
		if($this->modify_media_permissions_check( null ))
		{
			register_rest_route( $namespace, '/' . $base . '/(?P<id>[\d]+)', array(
			  array(
				'methods'             => WP_REST_Server::CREATABLE,
				'callback'            => array( $this, 'modify_media' ),
				'permission_callback' => array( $this, 'modify_media_permissions_check' ),
				'args'                => $this->get_endpoint_args_for_item_schema( false ),
			  ),
			));		
		}
  }
 
  /**
   * Update one item from the collection
   *
   * @param WP_REST_Request $request Full data about the request.
   * @return WP_Error|WP_REST_Response
   */
  public function modify_media( $request ) {
	
		$wp_id = filter_var_array($request->get_url_params(),array("id" => FILTER_VALIDATE_INT))["id"];

		if ( ! empty( $wp_id ) && ! empty( @$request->get_file_params()['file'] ) ) {
			$response = self::replace_image_from_upload( $wp_id, $request->get_file_params()['file'] );
			if ( ! empty( $response ) && ! empty( $response['url'] ) ) {
				$response['url'] = $response['url'] . '?v=' . current_time( 'timestamp' );
				return new WP_REST_Response( $response, 200 );
			}
		}
		
    return new WP_Error( 'cant-update', __( 'message', 'text-domain' ), array( 'status' => 500 ) );
  }
  
  /**
   * Check if a given request has access to update a specific item
   *
   * @param WP_REST_Request $request Full data about the request.
   * @return WP_Error|bool
   */
  public function modify_media_permissions_check( $request ) {
	  if(!is_user_logged_in()) return false;
	  
	  if($this->check_modmedia_cap)
	  {
		  $ret= current_user_can(self::MOD_MEDIA_CAP);
	  }
	  else
	  {
		  $ret= current_user_can('edit_posts');
	  }
	  
	  return $ret;
  }
 


/************************* End api Controller **********************************************/

	/**
	 * Return the data mapped in the _wp_attachment_metadata of an attachemnt.
	 *
	 * @param integer $initial_image_id The attachment ID.
	 * @return array
	 */
	public static function get_initial_image_metadata( $initial_image_id ) {
		$metadata = maybe_unserialize( get_post_meta( $initial_image_id, '_wp_attachment_metadata', true ) );
		return $metadata;
	}

	/**
	 * Remove the files that were mapped in the _wp_attachment_metadata of an attachemnt.
	 *
	 * @param integer $initial_image_id The attachment ID.
	 * @param string  $basedir          The base dir.
	 * @return void
	 */
	public static function remove_files_from_metadata( $initial_image_id, $basedir ) {
		$original_folder = '';
		$removable = array();

		$metadata = self::get_initial_image_metadata( $initial_image_id );
		if ( ! empty( $metadata['file'] ) ) {
			$original_folder = dirname( $metadata['file'] );
			$removable['original'] = $basedir . '/' . $metadata['file'];
		}
		if ( ! empty( $metadata['sizes'] ) ) {
			$sizes = wp_list_pluck( $metadata['sizes'], 'file' );
			foreach ( $sizes as $size => $file ) {
				$removable[ $size ] = $basedir . '/' . $original_folder . '/' . $file;
			}
		}
		if ( ! empty( $removable ) ) {
			foreach ( $removable as $size => $file ) {
				wp_delete_file( $file );
			}
		}
	}


	/**
	 * Return the processed guid, new and old metadata, if the image from the specified URL
	 * could be fetched and saved in the uploads directory as the attachment file.
	 *
	 * @param integer $initial_image_id The attachment ID.
	 * @param array  $file        file download array.
	 * @return boolean|array
	 */
	public static function replace_image_from_upload( $initial_image_id, $file ) {
		if ( ! empty( $initial_image_id ) && ! empty( $file ) ) {
			$new_file_content = '';

			// Let's check that the file was upoloaded.
			if ( empty( $file['erorr'] ) && ! empty( $file['tmp_name'] )  ) {
				$new_file_content = file_get_contents( $file['tmp_name'] );
			}

			if ( ! empty( ! empty( $file['tmp_name'] ) ) ) {
				@unlink( $file['tmp_name'] );
			}
			if ( ! empty( $new_file_content ) ) {
				return self::replace_image_from_file_content( $initial_image_id, $new_file_content );
			}
		}

		return false;
	}
	
	public static function get_attachment_path($attachment_id)
	{
		
		$image_path=get_attached_file($attachment_id);
		if($image_path !== false) return $image_path;

		$upload_dir = wp_upload_dir();
		$basedir = $upload_dir['basedir'];
				
		if($att_meta !== false && !empty($att_meta['file'])) {
			return $basedir . '/' . $att_meta['file'];
		}
		
		$post = get_post( $attachment_id );
		$meta = get_post_meta( $attachment_id, '_wp_attached_file', true );
		if ( ! empty( $meta ) ) {
			$image_path = $basedir . '/' . $meta;
		} else {
			$image_url=wp_get_attachment_url($attachment_id );
			$image_path = $upload_dir['path'] . '/' . basename( $image_url );
		}
		
		return $image_path;
	}

	/**
	 * Return the processed guid, new and old metadata, if the image from the specified URL
	 * could be fetched and saved in the uploads directory as the attachment file.
	 *
	 * @param integer $initial_image_id The attachment ID.
	 * @param string  $new_file_content The URL of the image used as the replacement.
	 * @return boolean|array
	 */
	public static function replace_image_from_file_content( $initial_image_id, $new_file_content ) {
		if ( ! empty( $initial_image_id ) && ! empty( $new_file_content ) ) {
			$upload_dir = wp_upload_dir();
			$basedir = $upload_dir['basedir'];

			$image_path = self::get_attachment_path( $initial_image_id);
			
			if ( ! empty( $image_path ) && file_exists( $image_path ) ) {
				// Cleanup existing files.
				self::remove_files_from_metadata( $initial_image_id, $basedir );
			}

			if ( @file_put_contents( $image_path, $new_file_content ) ) {
				// @TODO - maybe use wp_upload_bits when that function allows to specify the descrination, or we will have WP native hooks.
				$file_info = getimagesize( $image_path );

				if ( ! function_exists( 'wp_generate_attachment_metadata' ) ) {
					require_once( ABSPATH . 'wp-admin/includes/image.php' );
				}

				// Get the initial image info.
				$old_data = self::get_initial_image_metadata( $initial_image_id );
				$new_data = wp_generate_attachment_metadata( $initial_image_id, $image_path );
				if ( ! empty( $new_data['file'] ) ) {
					// This means that the metadata was generated for the new image.
					// Allow to filter and maybe override the new metadata.
					$new_data = apply_filters( 'wpdrive_replace_image_before_update_attachment_info', $new_data, $old_data );
					wp_update_attachment_metadata( $initial_image_id, $new_data );
				}

				$artdata = array(
					'ID' => (int) $initial_image_id,
					'guid' => wp_get_attachment_url( (int) $initial_image_id ),
					'post_mime_type' => $file_info['mime'],
				);

				$artdata = apply_filters( 'wpdrive_replace_image_before_update_attachment_post', $artdata );
				wp_update_post( $artdata );

				if ( function_exists( 'wp_update_image_subsizes' ) ) {
					// Attempt to regenerate subsizes.
					wp_update_image_subsizes( (int) $initial_image_id );
				}


				// Return the image URL.
				return array(
					'url'      => $artdata['guid'],
					'new_info' => $new_data,
					'old_info' => $old_data,
					'changed'  => true,
				);
			}
		}

		return false;
	}

	/**
	 * The actions to be executed when the plugin is updated.
	 *
	 * @return void
	 */
	public static function plugin_ver_check() {
		$opt = str_replace( '-', '_', self::PLUGIN_ID ) . '_db_ver';
		$dbv = get_option( $opt, 0 );
		if ( WPDRIVE_PLUGIN_VERSION !== (float) $dbv ) {
			update_option( $opt, WPDRIVE_PLUGIN_VERSION );
			self::activate_plugin();
		}
	}

}

// Instantiate the class.
$wpdrive_api = Wpdrive_api_extension::get_instance();

