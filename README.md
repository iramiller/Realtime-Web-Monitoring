# Realtime Website Monitoring

### Overview

The Realtime Website Monitoring project is a set of scripts in C# for consolidating 
request log data from IIS 7 web servers, Apache and Log4Net. For details on the
different components, how they work and what can be done with this software visit
the [Realtime Web Monitoring](http://rtm.aspectwest.net) website.

### Documentation
There is some documenation within the source files themselves but the best stuff is
within this repository itself as a [Webby](http://webby.rubyforge.org/) site.  You
can view this documentation by visiting the [Realtime Web Monitoring](http://rtm.aspectwest.net)
website.

### Source Code
The source for this project is in the `/source` folder in several subfolders.
The different parts of the project can be used together or independantly as required.
There are various open source projects used to make this software function.  Some of the
major ones are:

*  [MongoDB](http://www.mongodb.org) -- High performance data store for holding event data
*  [NEsper](http://esper.codehaus.org/about/nesper/nesper.html) -- realtime event processing
   that is used to create snapshots of realtime data using a simple SQL like language.
*  [Log4NET](http://logging.apache.org/log4net/) -- Simple and effective logging for .NET

The source code in most cases is C# targeting .NET 4.0.  The IIS7 module was written in C++.  All of
the source can be worked with in Visual Studio 2010.  Some of the source can be built in Mono,
however, the Native code for IIS and Windows services are not very useful on non-windows platforms
so this remains untested.

### Contributing
This goal of this project is a system that is simple and useful.  If you have some
enhancements or bug fixes you would like to contribute:

1.  Fork this repo.
1.  Make your changes
   1.  Respect the coding style used and try to blend in.
   1.  Have a look through some of the other files for examples.
1.  Send a pull request, include some details about your changes

### Adding to the documentation
If you would like to contribute to the documentation website then you will need to
install a few gems.

* gem install webby
* gem install ultraviolet
* gem install maruku
* gem install RedCloth
* gem install rdiscount

### License

This software is licensed under a BSD license to allow flexibility in its use.  Please
consider submiting your changes back to this project if you feel they would be useful
to others.