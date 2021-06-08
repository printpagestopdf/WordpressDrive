<?php
if (!defined('WP_UNINSTALL_PLUGIN')) {
    die;
}

class WpDriveUninstall {
	private $mod_media_role="modifymedia";
	private $mod_media_cap="modify_media";
	
	
	public function Uninstall()
	{
		$this->DeleteModMediaRole();
	}
	
	public function DeleteModMediaRole()
	{
		if(get_role($this->mod_media_role) == null) return true;
		
		$this->DeleteCapability($this->mod_media_cap);
		remove_role($this->mod_media_role);

	}

	public function DeleteCapability($cap)
	{
		global $wp_roles;
		
		foreach (array_keys($wp_roles->roles) as $role) 
		{
		  $wp_roles->remove_cap($role, $cap);
		}
		
	}
	
}

$uninst=new WpDriveUninstall();
$uninst->Uninstall();

?>