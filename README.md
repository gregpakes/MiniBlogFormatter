MiniBlogFormatter
=================

This is a console app for converting your existing blog to MiniBlog.

Current platforms supported:

* BlogEngine.NET
* DasBlog
* Wordpress

##Wordpress

I have changed the Wordpress support.  It is mostly specific to my blog, but it might be useful.

- Will download images previously uploaded to wordpress and put them in the "files" directory.  URLs are changed appropriately.
- Supports gists from wordpress in the format [gist id=xxxxxx].  It will convert them into the appropriate script tags.
- Implemented permalink remapping.  This requires you to make changes to the main MiniBlog application.  Contrib: http://www.colinsalmcorner.com/post/colins-alm-corner--updated-blog-engine
