[![Issue Stats](http://issuestats.com/github/fsprojects/Amazon.SimpleWorkflow.Extensions/badge/issue)](http://issuestats.com/github/fsprojects/Amazon.SimpleWorkflow.Extensions)
[![Issue Stats](http://issuestats.com/github/fsprojects/Amazon.SimpleWorkflow.Extensions/badge/pr)](http://issuestats.com/github/fsprojects/Amazon.SimpleWorkflow.Extensions)

Amazon.SimpleWorkflow.Extensions
==================================

Extensions to AmazonSDK's SimpleWorkflow capabilities to make it more intuitive to use for .Net developers.


### Maintainer(s)

- [@theburningmonk](https://github.com/theburningmonk)

The default maintainer account for projects under "fsprojects" is [@fsprojectsgit](https://github.com/fsprojectsgit) - F# Community Project Incubation Space (repo management)


### Amazon SWF ###
Amazon's Simple Workflow (SWF) service provides a scalable platform for automating processes, or workflows in your application.
It centers around the concept of tasks. Each task will fall into one of two categories:
* __Activity task__ - for doing actual work, e.g. processing an order, taking data from one location to another
* __Decision task__ - for deciding what's the next step in the workflow given its current state

Tasks are distributed to _'workers'_ with polling, and like Simple Queuing Service (SQS), each task can be received by one worker at a time and the worker has certain amount of time to complete the task and responds with a completion message or the task will be timed out (and can be retried up to some configured number of times). For long running activity tasks, the worker might also need to record periodic __heartbeats__, or the task might be marked as failed or timed out.

Every step of a workflow execution is recorded in SWF and you can retrieve the __full history of events__ that had occurred during a workflow either programmatically or via the Amazon Management Console.

You can also easily terminate or rerun a workflow with a few button clicks in the Amazon Management Console, should you choose to.

### Project Goal ###
The standard Amazon SDK provides a [AWS Flow Framework](http://docs.aws.amazon.com/amazonswf/latest/awsflowguide/welcome.html), but it's only available for Java.

The facilities provided by the .Net SDK is a straight mapping to the [actions](http://docs.aws.amazon.com/amazonswf/latest/apireference/Welcome.html) available on the SWF service. Using these actions alone, it's cumbersome to build any sort of useful process out of SWF because you need to take care of a lot of plumbing yourself, including:
* handle task polling
* handle exceptions (and retries) when polling tasks
* differentiate different types of tasks
* record heartbeats periodically
* respond completed message on successful completion
* respond failed message on exceptions during processing of a task
* ...

The goal of this project is to provide a framework that allows you to design and implement a workflow using SWF with as little friction in the development process as possible.

### Example usages ###
Check out the [Wiki](https://github.com/theburningmonk/Amazon.SimpleWorkflow.Extensions/wiki) page for a list of examples.

I also have a series of introductory posts on how to use the library to model workflows:
- [High Level Overview](http://theburningmonk.com/2013/02/making-amazon-simpleworkflow-simpler-to-work-with/)
- [Part 1 - Hello World] (http://theburningmonk.com/2013/02/introduction-to-aws-simpleworkflow-extensions-part-1-hello-world-example/)
- [Part 2 - Beyond Hello World] (http://theburningmonk.com/2013/02/introduction-to-aws-simpleworkflow-extensions-part-2-beyond-hello-world/)
- [Part 3 - Parallelizing activities] (http://theburningmonk.com/2013/02/introduction-to-aws-simpleworkflow-extensions-part-3-parallelizing-activities/)
- Part 4 - Child Workflows (coming soon)
- Part 5 - Retries (coming soon)

### Nuget ###
Download and install using [NuGet](https://nuget.org/packages/Amazon.SimpleWorkflow.Extensions). 
[![NuGet Status](http://img.shields.io/nuget/v/Amazon.SimpleWorkflow.Extensions.svg?style=flat)](https://www.nuget.org/packages/Amazon.SimpleWorkflow.Extensions/)

<a href="https://nuget.org/packages/Amazon.SimpleWorkflow.Extensions"><img src="http://theburningmonk.com/images/swf-extension-nuget-install.png" alt="NuGet package"/></a>

You can view the full release notes [here](https://github.com/theburningmonk/Amazon.SimpleWorkflow.Extensions/wiki/Release-Notes).

### Keep in touch
Please follow the official twitter account [@swf_extensions](https://twitter.com/swf_extensions) for updates, and feel free to send me any feedbacks or questions you have about the project.
