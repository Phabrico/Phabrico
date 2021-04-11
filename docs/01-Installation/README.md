# Installation

The MSI installer for Windows can be found here: https://github.com/Phabrico/Phabrico/releases/latest

![image-20210411101554975](msi-installer-01.png) 
Click *Next*



![image-20210411102642590](msi-installer-02.png) 
Set the path to where Phabrico should be installed to and click *Next* afterwards.
 Note: the local database itself will be installed in the user's document directory.



![image-20210411103037509](msi-installer-03.png) 
You can extend your Phabrico with some plugins.
 Click *Install* afterwards.



![image-20210411103251481](msi-installer-04.png) 
Phabrico will be installed.



If everything went OK, you should see this dialog:
![image-20210411103445072](msi-installer-05.png) 
After you click *Finish* to close the installer, your browser will open the Phabrico website.



# Configuring the connection between Phabrico and Phabricator

 The first time you browse to the Phabrico website, you will see this screen:

![image-20210411103907992](configuration-01.png) 
In the top right corner, you can change the language of the application.

You need to enter a username and a password.
These credentials are only used by Phabrico and don't need to be the same as the ones you use for Phabricator.

The Phabricator URL is the web address where the Phabricator website is located.

The Conduit API token is a token which allows Phabrico to communicate with Phabricator.
This token is a personal token which can be retrieved in Phabricator via the *Settings* menu in your personal menu at the top right: ![image-20210411105638160](configuration-02.png)

![image-20210411110426452](configuration-03.png)

After clicking the green *Create User* button in Phabrico, a connection will be established with the Phabricator webserver.
It will download all available projects and user accounts from the Phabricator server.

![image-20210411111026032](configuration-04.png)
These will be used later on to configure the synchronization process between Phabrico and Phabricator.

After the first synchronization process between Phabrico and Phabricator, you will get a short introduction:
![image-20210411111315207](configuration-05.png)
