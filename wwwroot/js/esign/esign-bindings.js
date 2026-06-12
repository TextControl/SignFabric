(function () {
    function getActionElement(target) {
        return target.closest("[data-esign-action]");
    }

    function currentContractId() {
        return TextControl.esign.currentContract().contract.contractID;
    }

    function runAction(element) {
        var action = element.dataset.esignAction;

        switch (action) {
            case "add-file":
                TextControl.esign.addFile(element.files);
                break;
            case "add-contract":
                TextControl.esign.addContract(element.files);
                break;
            case "add-template":
                TextControl.esign.addTemplate(element.files);
                break;
            case "open-upload":
                document.getElementById(element.dataset.target).click();
                break;
            case "create-template":
                TextControl.esign.createNewTemplate(element.dataset.nameInput);
                break;
            case "submit-recipient":
                TextControl.esign.submitRecipient(element.dataset.envelopeId || currentContractId(), element.dataset.type);
                break;
            case "confirm-recipients":
                TextControl.esign.confirmRecipients(element.dataset.envelopeId);
                break;
            case "apply-field-assignments":
                TextControl.esign.applyRecipientFieldAssignments();
                break;
            case "next-step":
                TextControl.esign.nextStep(element.dataset.target);
                break;
            case "submit-envelope":
                TextControl.esign.submitEnvelope(element.dataset.envelopeId);
                break;
            case "submit-contract":
                TextControl.esign.submitContract(element.dataset.contractId || currentContractId());
                break;
            case "submit-signature-box":
                TextControl.esign.submitSignaturebox(element.dataset.envelopeId);
                break;
            case "accept-all":
                if (typeof acceptAll === "function") acceptAll();
                break;
            case "make-changes":
                if (typeof makeChanges === "function") makeChanges();
                break;
            case "copy-link":
                TextControl.esign.copyLink(element.dataset.target);
                break;
            case "load-template-editor":
                TextControl.esign.loadTemplateEditor(element.dataset.templateId);
                break;
            case "load-contract-editor":
                TextControl.esign.loadContractEditor(element.dataset.contractId || currentContractId());
                break;
            case "continue-contract-recipient":
                TextControl.esign.continueContractRecipient();
                break;
            case "add-section":
                TextControl.esign.addSection();
                break;
            case "load-editor":
                TextControl.esign.loadEditor(element.dataset.envelopeId);
                break;
            case "insert-text-frame":
                TextControl.esign.insertTextFrame(element.dataset.signerId, element.dataset.signerName);
                updatePlacementHint(element);
                break;
            case "insert-text-form-field":
                TextControl.esign.insertTextFormField();
                updatePlacementHint(element);
                break;
            case "insert-checkbox":
                TextControl.esign.insertCheckbox();
                updatePlacementHint(element);
                break;
            case "insert-dropdown":
                TextControl.esign.insertDropDownFormField();
                updatePlacementHint(element);
                break;
            case "insert-date-picker":
                TextControl.esign.insertDatePicker();
                updatePlacementHint(element);
                break;
            case "insert-auto-fill-field":
                TextControl.esign.insertAutoFillField(element.dataset.fieldType, element.dataset.fieldLabel);
                updatePlacementHint(element);
                break;
            case "open-signature-area-wizard":
                TextControl.esign.openSignatureAreaWizard();
                break;
            case "insert-signature-area":
                TextControl.esign.insertSignatureArea();
                updatePlacementHint(element);
                break;
            case "save-editor-document":
                if (typeof saveDocument === "function") saveDocument();
                break;
            case "update-section-name":
                TextControl.esign.updateSectionName();
                break;
            case "delete-section":
                TextControl.esign.deleteSection();
                break;
            case "insert-merge-field":
                TextControl.esign.insertMergeField();
                break;
            case "insert-date-field":
                TextControl.esign.insertDateField();
                break;
        }
    }

    function updatePlacementHint(element) {
        var hint = document.getElementById("fieldPlacementHint");
        if (!hint || !element.dataset.placementHint) return;

        hint.textContent = element.dataset.placementHint;
    }

    document.addEventListener("click", function (event) {
        var toggle = event.target.closest(".editor-sidebar-toggle");
        if (!toggle || toggle.getAttribute("aria-expanded") !== "true") return;

        event.preventDefault();
        event.stopImmediatePropagation();
    }, true);

    document.addEventListener("click", function (event) {
        var element = getActionElement(event.target);
        if (!element || element.matches("input[type=file]")) return;

        event.preventDefault();
        runAction(element);
    }, true);

    document.addEventListener("change", function (event) {
        var element = getActionElement(event.target);
        if (!element || !element.matches("input[type=file]")) return;

        runAction(element);
    });

    document.addEventListener("dragover", function (event) {
        if (!event.target.closest("[data-esign-dropzone]")) return;
        TextControl.esign.dragOverHandler(event);
    });

    document.addEventListener("drop", function (event) {
        if (!event.target.closest("[data-esign-dropzone]")) return;
        TextControl.esign.dropHandler(event);
    });
}());
