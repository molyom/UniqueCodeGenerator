﻿/*The MIT License (MIT)

Copyright (c) 2020 Molyom  

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
*/

// Removes the plugin step from an entity, if there are no registered autonumber records

using System;
using System.Linq;
using Microsoft.Xrm.Sdk;

namespace Molyom
{
	public class DeleteUniqueCode : MolyomPlugin
	{
		//
		// This plugin is executed when an AutoNumber record is deleted, it will remove the plugin steps from the associated entity
		//
		// Registration details:
		// Message: Delete
		// Primary Entity: molyom_uniquecodegenerator
		// User context: SYSTEM
		// Event Pipeline: Post
		// Mode: Async
		// Config: none
		//
		// PreImage:
		// Name: PreImage
		// Alias: PreImage
		// Attributes: molyom_entityname, molyom_attributename
		//
		public DeleteUniqueCode()
		{
			//this.RegisteredEvents.Add(new Tuple<int,string,string,Action<LocalPluginContext>>(PostOperation, DELETEMESSAGE, "entityname", new Action<LocalPluginContext>(Execute)));
			RegisterEvent(Constants.PipelineStage.PreOperation, Constants.PipelineMessage.Delete, "molyom_uniquecodegenerator", Execute);
		}

		protected void Execute(LocalPluginContext context)
		{
			var triggerEvent = context.PreImage.Contains("molyom_triggerevent") && context.PreImage.GetAttributeValue<OptionSetValue>("molyom_triggerevent").Value == 1 ? 1 : 0;

			var remainingAutoNumberList = context.OrganizationDataContext.CreateQuery("molyom_uniquecodegenerator")
																		 .Where(s => s.GetAttributeValue<string>("molyom_entityname").Equals(context.PreImage.GetAttributeValue<string>("molyom_entityname")))
																		 .Select(s => new { Id = s.GetAttributeValue<Guid>("molyom_uniquecodegeneratorid"), TriggerEvent = s.Contains("molyom_triggerevent") ? s.GetAttributeValue<OptionSetValue>("molyom_triggerevent").Value : 0  })
																		 .ToList();

			if (remainingAutoNumberList.Any(s => s.TriggerEvent == triggerEvent ))  // If there are still other autonumber records on this entity, then do nothing.
			{
				return;  
			}

			// Find and remove the registerd plugin
			var pluginName = string.Format(CreateUniqueCode.PluginName, context.PreImage.GetAttributeValue<string>("molyom_entityname"));

			if (context.PreImage.Contains("molyom_triggerevent") && context.PreImage.GetAttributeValue<OptionSetValue>("molyom_triggerevent").Value == 1)
			{
				pluginName += " Update";
			}

			var pluginStepList = context.OrganizationDataContext.CreateQuery("sdkmessageprocessingstep")
																.Where(s => s.GetAttributeValue<string>("name").Equals(pluginName))
																.Select(s => s.GetAttributeValue<Guid>("sdkmessageprocessingstepid"))
																.ToList();

			if (!pluginStepList.Any())  // Plugin is already deleted, nothing to do here.
			{
				return;  
			}

            // Delete all images
		    var images = context.OrganizationDataContext.CreateQuery("sdkmessageprocessingstepimage")
		        .Where(s => s.GetAttributeValue<Guid>("sdkmessageprocessingstepid").Equals(pluginStepList.First()))
		        .Select(s => s.GetAttributeValue<Guid>("sdkmessageprocessingstepimageid"))
		        .ToList();

		    foreach (var image in images)
		    {
		        context.OrganizationService.Delete("sdkmessageprocessingstepimage", image);
            }

            // Delete plugin step
            context.OrganizationService.Delete("sdkmessageprocessingstep", pluginStepList.First());
		}
	}
}
