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