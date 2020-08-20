/*The MIT License (MIT)

Copyright (c) 2020

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
FITNESS FOR A PARTICULAR PURPOSE AND NON INFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
*/

var Molyom = (function (Molyom) {

	Molyom.UniqueCodeOnload = function onLoad(executionContext) {
		var formContext = executionContext.getFormContext();
		if (formContext.ui.getFormType() != 1) {
			formContext.getControl("molyom_entityname").setDisabled(true);
			formContext.getControl("molyom_attributename").setDisabled(true);
			formContext.getControl("molyom_triggerevent").setDisabled(true);
			Molyom.GeneratePreview(formContext);
		}
		formContext.getAttribute("molyom_preview").setSubmitMode("never");
	}

	Molyom.GeneratePreview = function generatePreview(executionContext) {
		var formContext = executionContext.getFormContext();
		var Prefix = formContext.getAttribute("molyom_prefix").getValue() || "";
		var Suffix = formContext.getAttribute("molyom_suffix").getValue() || "";
		var NextNumber = formContext.getAttribute("molyom_nextcode").getValue() || 1;

		formContext.getAttribute("molyom_preview").setValue(Prefix + zeroPad(NextNumber) + Suffix);
	}

	Molyom.onChangeNextCode = function onchageNextCode(executionContext) {
		var formContext = executionContext.getFormContext();
		formContext.getAttribute("molyom_nextcode").setValue(formContext.getAttribute("molyom_nextcode").getValue().toLowerCase());
	}

	function zeroPad(num, digits) {
		digits = digits || Xrm.Page.getAttribute("molyom_characterlength").getValue() || 1;
		return ('0000000000000000' + num).substr(-digits);
	}
	return Molyom;
}(Molyom || {}));