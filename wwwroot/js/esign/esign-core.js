var TextControl = (function (tx) {

    var currentEnvelope;
    var currentContract;
    var pendingFieldAssignment;

    function showWaitState() {
        $(".waitstate").addClass("visible");
    }

    function hideWaitState() {
        $(".waitstate").removeClass("visible");
    }

    function readResponse(response) {
        if (!response.ok) {
            return response.text().then(function (body) {
                var message = response.statusText;

                if (body) {
                    try {
                        var error = JSON.parse(body);
                        message = error.error || error.message || body;
                    }
                    catch (_) {
                        message = body;
                    }
                }

                throw new Error(message);
            });
        }

        var contentType = response.headers.get("content-type") || "";

        if (contentType.indexOf("application/json") !== -1)
            return response.json();

        return response.text();
    }

    function request(url, options) {
        var requestOptions = options || {};
        var showWait = requestOptions.wait !== false;
        var headers = new Headers(requestOptions.headers || {});

        if (!headers.has("X-Requested-With")) {
            headers.set("X-Requested-With", "XMLHttpRequest");
        }

        delete requestOptions.wait;
        requestOptions.credentials = requestOptions.credentials || "same-origin";
        requestOptions.headers = headers;

        if (showWait)
            showWaitState();

        return fetch(url, requestOptions)
            .then(readResponse)
            .finally(function () {
                if (showWait)
                    hideWaitState();
            });
    }

    function showError(error, fallback) {
        var message = error && error.message ? error.message : fallback;
        TextControl.esign.showToast(message || "Something went wrong. Please try again.", "danger");
    }

    function postJson(url, data, wait) {
        return request(url, {
            method: "POST",
            headers: {
                "Content-Type": "application/json"
            },
            body: data === undefined ? undefined : JSON.stringify(data),
            wait: wait
        });
    }

    function loadHtml(selector, url) {
        return request(url, {
            method: "GET",
            headers: {
                "X-Requested-With": "XMLHttpRequest"
            },
            wait: false
        }).then(function (html) {
            var target = document.querySelector(selector);

            if (!target)
                return;

            target.innerHTML = html;

            return runScripts(target).then(function () {
                target.dispatchEvent(new CustomEvent("esign:partial-loaded", {
                    bubbles: true
                }));
            });
        });
    }

    function runScripts(root) {
        var scripts = Array.prototype.slice.call(root.querySelectorAll("script"));

        return scripts.reduce(function (chain, script) {
            return chain.then(function () {
                return new Promise(function (resolve) {
                    var replacement = document.createElement("script");

                    Array.prototype.slice.call(script.attributes).forEach(function (attribute) {
                        replacement.setAttribute(attribute.name, attribute.value);
                    });

                    replacement.async = false;

                    if (script.src) {
                        replacement.onload = resolve;
                        replacement.onerror = resolve;
                    }
                    else {
                        replacement.text = script.text;
                    }

                    script.parentNode.replaceChild(replacement, script);

                    if (!script.src)
                        resolve();
                });
            });
        }, Promise.resolve());
    }

    tx.esign = {

        currentContract: function () {
            return currentContract;
        },

        loadPartial: function (selector, url) {
            return loadHtml(selector, url);
        },

        deleteSection: function () {
            TXTextControl.subTextParts.getItem(section => {
                TXTextControl.subTextParts.remove(section, true, false);
                TextControl.esign.updateSectionList();
            })
        },

        updateSectionName: function () {
            TXTextControl.selection.getStart(function (curSelStart) {

                TXTextControl.paragraphs.getItemAtTextPosition(curSelStart, function (par) {
                    par.getText(text => {

                        var title = text.trim();

                        if (title.length > 30)
                            title = title.substr(0, 30) + "[...]";

                        document.getElementById("section-name").value = title;
                    })
                })

            })
        },

        updateSectionList: function () {

            $("#availableSections").empty();

            TXTextControl.subTextParts.forEach(section => {

                section.setHighlightMode(TXTextControl.HighlightMode.Activated, function () {
                    section.getName(name => {

                        section.getData(id => {
                            $("#availableSections").append('<a onclick="TextControl.esign.activateSection(\'' + id + '\')" class="list-group-item list-group-item-action toolbox-item"><i class="bi bi-layout-text-window-reverse left" aria-hidden="true"></i><p class="sidebar-small">' + name + '</p></a>');
                        })
                    })
                })

            })
        },

        activateSection: function (id) {
            TXTextControl.subTextParts.forEach(part => {
                part.getData(data => {
                    if (data === id) {
                        part.getStart(start => {
                            part.scrollTo();
                            TXTextControl.selection.setStart(start - 1, function () { TXTextControl.focus(); });
                            
                            return;
                        })
                    }
                })
            })
        },

        addSection: function () {

            TXTextControl.selection.getStart(function (curSelStart) {
                TXTextControl.selection.getLength(function (curSelLength) {

                    var range = {
                        start: curSelStart + 1,
                        length: curSelLength,
                    };

                    var fullRange = {
                        start: 0,
                        length: 0,
                    };

                    var name = document.getElementById("section-name").value;

                    TXTextControl.paragraphs.getItemAtTextPosition(range.start, function (par) {
                        par.getStart(start => {
                            fullRange.start = start;

                            TXTextControl.paragraphs.getItemAtTextPosition(range.start + range.length, function (secondPar) {
                                secondPar.getStart(startSecond => {

                                    secondPar.getLength(length => {
                                        fullRange.length = (startSecond + length) - fullRange.start;

                                        TXTextControl.subTextParts.add(name, 0, fullRange.start, fullRange.length - 1, part => {

                                            if (part.addResult === 1) {
                                                part.subTextPart.setHighlightColor("rgba(0,255,0,.5)", function () {
                                                    part.subTextPart.setData(uuidv4(), function () {
                                                        TXTextControl.focus();
                                                        TextControl.esign.updateSectionList();
                                                    });
                                                });
                                            }
                                            else {
                                                TextControl.esign.showToast("Section cannot be inserted at this location.")
                                            }

                                            
                                        });
                                        
                                    })

                                })
                            });
                        })
                    });

                   
                });
            });
            
        },

        addFile: function (files) {
            var file = files[0];

            var data = new FormData();
            data.append(file.name, file);
            appendSigningCertificate(data);

            uploadDocument(data);
        },

        addContract: function (files) {
            var file = files[0];

            var data = new FormData();
            data.append(file.name, file);

            uploadContract(data);
        },

        addTemplate: function (files) {
            var file = files[0];

            var data = new FormData();
            data.append(file.name, file);

            uploadTemplate(data);
        },

        getDocument: function (documentid, typeString) {
            request("/" + typeString + "/document/" + documentid, {
                method: "GET"
            })
                .then(function (data) {
                    TXTextControl.loadDocument(32, data, function () {
                        TextControl.esign.checkTextFrames();
                    });
                })
                .catch(function (error) {
                    showError(error, "The document could not be loaded.");
                });
        },

        getContract: function (documentid) {
            request("/collaboration/document/" + documentid, {
                method: "GET"
            })
                .then(function (data) {
                    TXTextControl.loadDocument(32, data);
                })
                .catch(function (error) {
                    showError(error, "The document could not be loaded.");
                });
        },

        loadEditor: function (documentid) {
            if (typeof TXTextControl !== 'undefined')
                TXTextControl.removeFromDom();

            loadHtml("#editor", "/envelopes/edit/" + documentid + "?handler=EditPartial");
            $("#editor").addClass("action");
            $("#main").addClass("inactive");
            $(".navbar").removeClass("fixed-top");
        },

        loadTemplateEditor: function (documentid) {
            if (typeof TXTextControl !== 'undefined')
                TXTextControl.removeFromDom();

            loadHtml("#editor", "/templates/edit/" + documentid + "?handler=EditTemplatePartial");
            $("#editor").addClass("action");
            $("#main").addClass("inactive");
            $(".navbar").removeClass("fixed-top");
        },

        loadContractEditor: function (documentid) {
            if (typeof TXTextControl !== 'undefined')
                TXTextControl.removeFromDom();

            loadHtml("#editor", "/contracts/edit/" + documentid + "?handler=EditPartial");
            $("#editor").addClass("action");
            $("#main").addClass("inactive");
            $(".navbar").removeClass("fixed-top");
        },

        insertMergeField: function () {
            var mergeField = new TXTextControl.MergeField;
            mergeField.name = "mergefield";
            mergeField.text = "«mergefield»";
            TXTextControl.addMergeField(mergeField);
        },

        insertDateField: function () {
            var dateField = new TXTextControl.DateField;
            dateField.name = "date";
            TXTextControl.addMergeField(dateField);
        },

        insertAutoFillField: function (fieldType, label) {
            var mergeField = new TXTextControl.MergeField;
            mergeField.name = getAutoFillFieldName(fieldType);
            mergeField.text = "«" + label + "»";
            TXTextControl.addMergeField(mergeField);
        },

        openSignatureAreaWizard: function () {
            var modalElement = document.getElementById("signatureAreaWizardModal");
            var signers = getAvailableSigners();

            if (!modalElement) {
                return;
            }

            if (!signers.length) {
                TextControl.esign.showToast("Add at least one recipient before inserting a signature area.", "warning");
                return;
            }

            setDefaultSignatureAreaSignerSelection(signers);
            showForegroundModal(modalElement);
        },

        insertSignatureArea: function () {
            var options = getSignatureAreaOptions();
            var signers = resolveSignatureAreaSigners(options.selectedSignerIds);
            var modalElement = document.getElementById("signatureAreaWizardModal");

            if (!signers.length) {
                TextControl.esign.showToast("Select at least one recipient before inserting a signature area.", "warning");
                return;
            }

            if (modalElement) {
                var modal = bootstrap.Modal.getInstance(modalElement);
                if (modal) {
                    modal.hide();
                }
            }

            insertSignatureAreaTable(signers, options)
                .then(function () {
                    TXTextControl.focus();
                })
                .catch(function (error) {
                    showError(error, "The signature area could not be inserted.");
                });
        },

        insertTextFrame: function (id, name) {
            TXTextControl.selection.getStart(function (start) {
                TXTextControl.signatureFields.addAnchored(
                    { width: 4000, height: 2000 },
                    TXTextControl.HorizontalAlignment.Left,
                    start, // TextPosition
                    TXTextControl.TextFrameInsertionMode.AboveTheText,

                    (addedTextFrame) => {
                        addedTextFrame.setName(id ? "txsign_" + id : "txsign_unassigned:" + uuidv4());
                        TextControl.esign.checkTextFrames();
                    }
                );
            });
        },

        assignSelectedSignatureField: function (id) {
            if (!id) {
                TextControl.esign.showToast("Select a recipient before assigning the signature field.", "warning");
                return;
            }

            TXTextControl.signatureFields.getItem(function (signatureField) {
                if (!signatureField || typeof signatureField.setName !== "function") {
                    TextControl.esign.showToast("Select a signature field in the document first.", "warning");
                    return;
                }

                var completed = false;
                function finishAssignment() {
                    if (completed) {
                        return;
                    }

                    completed = true;
                    TextControl.esign.checkTextFrames();
                    TextControl.esign.highlightSignatureButton("txsign_" + id);
                }

                signatureField.setName("txsign_" + id, finishAssignment);
                window.setTimeout(finishAssignment, 0);
            }, function () {
                TextControl.esign.showToast("Select a signature field in the document first.", "warning");
            });
        },

        checkTextFrames: function () {
            $(".toolbox-item-small").removeClass("checked");

            TXTextControl.signatureFields.forEach(function (signatureField) {
                signatureField.getName(function (name) {
                    var button = document.getElementById(name);
                    if (button) {
                        button.classList.add("checked");
                    }
                });
            });

        },

        clearActiveSignatureButton: function () {
            $(".toolbox-item-small").removeClass("active-signature");
        },

        highlightSignatureButton: function (name) {
            TextControl.esign.clearActiveSignatureButton();

            if (!name) {
                return;
            }

            var button = document.getElementById(name);
            if (button) {
                button.classList.add("active-signature");
            }
        },

        insertTextFormField: function () {
            TXTextControl.formFields.getCanAdd(canAdd => {
                if (canAdd) {

                    var formOwner = getSelectedFormOwner();

                    // Add form field
                    TXTextControl.formFields.addTextFormField(3000, ff => {
                        ff.setName(formOwner + ":" + uuidv4());
                    });

                } else {
                    TextControl.esign.showToast("Form field cannot be inserted at this location.");
                }
            });
        },

        insertDropDownFormField: function () {
            TXTextControl.formFields.getCanAdd(canAdd => {
                if (canAdd) {

                    var formOwner = getSelectedFormOwner();

                    // Add form field
                    TXTextControl.formFields.addSelectionFormField(3000, ff => {
                        var items = ["Entry1", "Entry2"];

                        ff.setEditable(false);
                        ff.setName(formOwner + ":" + uuidv4());
                        ff.setItems(items);
                        ff.setSelectedIndex(1);
                    });
                } else {
                    TextControl.esign.showToast("Form field cannot be inserted at this location.");
                }
            });
        },

        insertCheckbox: function () {
            TXTextControl.formFields.getCanAdd(canAdd => {
                if (canAdd) {

                    var formOwner = getSelectedFormOwner();

                    // Add form field
                    TXTextControl.formFields.addCheckFormField(true, ff => {
                        ff.setName(formOwner + ":" + uuidv4());
                    });

                } else {
                    TextControl.esign.showToast("Form field cannot be inserted at this location.");
                }
            });
        },

        insertDatePicker: function () {
            TXTextControl.formFields.getCanAdd(canAdd => {
                if (canAdd) {

                    var formOwner = getSelectedFormOwner();

                    // Add form field
                    TXTextControl.formFields.addDateFormField(1000, ff => {
                        ff.setName(formOwner + ":" + uuidv4());
                    });
                } else {
                    TextControl.esign.showToast("Form field cannot be inserted at this location.");
                }
            });
        },

        copyLink: function (link) {

            var copyText = document.getElementById(link);

            copyText.select();
            copyText.setSelectionRange(0, 99999);

            if (navigator.clipboard) {
                navigator.clipboard.writeText(copyText.value);
            }
            else {
                document.execCommand("copy");
            }

            TextControl.esign.showToast("Value copied to clipboard!");
        },

        saveEditor: function (documentContent, envelopeId) {
            var signModel = { "document": documentContent };

            TextControl.esign.showToast("Saving...");

            postJson("/envelope/saveDocument/" + envelopeId, signModel, false)
                .then(function () {
                    TextControl.esign.showToast("Document successfully saved!");
                    $("#editor").removeClass("action");
                    $("#main").removeClass("inactive");
                    $(".navbar").addClass("fixed-top");
                    loadHtml("#collapseSignature", "/envelopes/edit/" + envelopeId + "?handler=SignatureBoxPartial");
                })
                .catch(function (error) {
                    showError(error, "The document could not be saved.");
                });
        },

        saveTemplate: function (documentContent, envelopeId) {
            var signModel = { "document": documentContent };

            postJson("/template/saveDocument/" + envelopeId, signModel, false)
                .then(function () {
                    refreshTemplateSummary(envelopeId);
                    $("#editor").removeClass("action");
                    $("#main").removeClass("inactive");
                    $(".navbar").addClass("fixed-top");

                    tx.esign.getApplicationFields(envelopeId);
                    
                })
                .catch(function (error) {
                    showError(error, "The template could not be saved.");
                });
        },

        saveContract: function (documentContent, contractId, owner) {
            var collaborationModel = { "document": documentContent };

            TextControl.esign.showToast("Saving...");

            postJson("/collaboration/saveDocument/" + contractId + "?owner=" + encodeURIComponent(owner), collaborationModel, false)
                .then(function (status) {
                    window.location.href = status;
                })
                .catch(function (error) {
                    showError(error, "The document could not be saved.");
                });
        },

        saveContractEditor: function (documentContent, contractId) {
            var signModel = { "document": documentContent };

            postJson("/contract/saveDocument/" + contractId, signModel, false)
                .then(function () {
                    refreshContractSummary(contractId);
                    $("#editor").removeClass("action");
                    $("#main").removeClass("inactive");
                    $(".navbar").addClass("fixed-top");
                    TextControl.esign.continueContractRecipient();
                })
                .catch(function (error) {
                    showError(error, "The contract could not be saved.");
                });
        },

        submitEnvelope: function (envelopeId) {
            saveWorkflow(envelopeId)
                .then(function (envelope) {
                    currentEnvelope = envelope;
                    return postJson("/envelope/submit/" + envelopeId);
                })
                .then(function () {
                    TextControl.esign.showToast("Envelope successfully sent!");
                    $("#statusReview").addClass("status-check");
                    $("#submitButtons").hide();
                    $("#readyButton").removeClass("visually-hidden");
                })
                .catch(function (error) {
                    showError(error, "The envelope could not be sent.");
                });
        },

        submitContract: function (contractId) {
            postJson("/contract/submit/" + contractId)
                .then(function () {
                    TextControl.esign.showToast("Envelope successfully sent!");
                    $("#statusReview").addClass("status-check");
                    $("#submitButtons").hide();
                    $("#readyButton").removeClass("visually-hidden");
                })
                .catch(function (error) {
                    showError(error, "The contract could not be sent.");
                });
        },

        getApplicationFields: function (templateId, canRequestSignatures) {
            postJson("/template/getfields/" + templateId)
                .then(function (status) {

                    $("#tx-fields").empty();

                    $("#tx-fields").append("<form id='submitfields' method='post' action='/template/instance/" + templateId + "' >");

                    status = status.filter(function (field) {
                        return !isAutoFillFieldName(field.name);
                    });

                    if (status.length === 0) {
                        $("#submitfields").append("<p>No merge fields found.</p>");
                    }
                    else {

                        status.forEach(function (field) {
                            $("#submitfields").append("<div class='mt-2'><label for='" + field.name + "' class='form-label'>" + field.name + "</label><input class='form-control' type='text' placeholder='Complete this field' name='" + field.name + "' id='" + field.name + "' /></div>");
                        });

                    }

                    $("#submitfields").append("<div class='mt-4 d-flex flex-wrap gap-2'></div>");

                    if (canRequestSignatures !== false) {
                        $("#submitfields > div:last-child").append("<button class='btn btn-warning' type='submit'><strong>Create Envelope</strong></button>");
                    }
                    else {
                        $("#submitfields > div:last-child").append("<button class='btn btn-outline-secondary' type='button' disabled>Create Envelope</button>");
                    }

                    $("#submitfields > div:last-child").append("<button class='btn btn-success' type='submit' formaction='/template/contract/" + templateId + "'><strong>Create Contract</strong></button>");
                })
                .catch(function (error) {
                    showError(error, "The template fields could not be loaded.");
                });
        },

        submitSignaturebox: function (envelopeId) {
            $("#statusSignature").addClass("status-check");
            $("#statusReview").addClass("status-active");
            TextControl.esign.showToast("Signature box successfully updated!");
            TextControl.esign.nextStep('collapseReview');

            $("#reviewRecipient").empty();

            currentEnvelope.signers.forEach(function (signer) {
                var authentication = signer.requireEmailOtp ? " - e-mail OTP required" : "";
                $("#reviewRecipient").append("<li>" + signer.email + authentication + "</li>")
            });

        },

        showToast: function (statusText, variant) {
            if (variant !== "danger" && variant !== "warning") {
                return;
            }

            showMessageModal(statusText, variant);
        },

        removeRecipient: function (envelopeId, type, email, name) {
            var url = "/" + type + "/removerecipient/" + envelopeId;

            var data = { "name": name, "email": email };

            postJson(url, data, false)
                .then(function (envelope) {
                    currentEnvelope = envelope;
                    TextControl.esign.showToast("Recipient successfully removed!");

                    updateRecipients(currentEnvelope.signers, envelopeId, type);

                    $("#recipientAlreadyAdded").addClass("collapse");
                })
                .catch(function (error) {
                    showError(error, "The recipient could not be removed.");
                });
        },

        receiveRecipients: function (envelopeId, type) {
            var url = "/" + type + "/receiverecipients/" + envelopeId;

            request(url, {
                method: "GET",
                wait: false
            })
                .then(function (envelope) {
                    currentEnvelope = envelope;

                    updateRecipients(currentEnvelope.signers, envelopeId, type);

                    $("#signerName").val("");
                    $("#signerEmail").val("");
                })
                .catch(function (error) {
                    showError(error, "The recipients could not be loaded.");
                    $("#recipientAlreadyAdded").removeClass("collapse");
                });
        },

        submitRecipient: function (envelopeId, type) {
            var forms = document.querySelectorAll('.needs-validation')

            var url = "/" + type + "/updaterecipient/" + envelopeId;

            Array.prototype.slice.call(forms)
                .forEach(function (form) {

                    form.addEventListener('submit', function (event) {
                        event.preventDefault()
                        event.stopPropagation()
                    }, false)

                    if (!form.checkValidity()) {
                        form.classList.add('was-validated');
                        return;
                    }
                    else {
                        var name = $("#signerName").val();
                        var email = $("#signerEmail").val();
                        var requireEmailOtpInput = document.getElementById("requireEmailOtp");
                        var requireEmailOtp = !!(requireEmailOtpInput && requireEmailOtpInput.checked);
                        var role = isComplexWorkflow() ? parseInt((document.getElementById("recipientRole") || {}).value || "0", 10) || 0 : 0;
                        var orderInput = document.getElementById("recipientRoutingOrder");
                        var routingOrder = role === 3 ? 0 : isComplexWorkflow() ? Math.max(parseInt(orderInput && orderInput.value || "1", 10) || 1, 1) : 1;

                        var data = {
                            "name": name,
                            "email": email,
                            "role": role,
                            "routingOrder": routingOrder,
                            "requireEmailOtp": requireEmailOtp
                        };

                        postJson(url, data, false)
                            .then(function (envelope) {
                                currentEnvelope = envelope;
                                TextControl.esign.showToast("Recipient successfully updated!");

                                if (type === "envelope") {
                                    updateRecipients(currentEnvelope.signers, envelopeId, type);

                                    $("#recipientAlreadyAdded").addClass("collapse");

                                    $("#signerName").val("");
                                    $("#signerEmail").val("");
                                    $("#requireEmailOtp").prop("checked", false);
                                }
                                else if (type === "contract") {
                                    $("#statusRecipient").addClass("status-check");
                                    TextControl.esign.nextStep('collapseReview');

                                    $("#reviewRecipient").text(currentEnvelope.signer.email);
                                }
                            })
                            .catch(function (error) {
                                showError(error, "The recipient could not be updated.");
                                $("#recipientAlreadyAdded").removeClass("collapse");
                            });
                    }
                });
        },

        confirmRecipients: function (envelopeId) {
            return saveWorkflow(envelopeId)
                .then(function () {
                    return ensureRecipientFieldAssignments(envelopeId);
                })
                .then(function (assigned) {
                    if (!assigned) return;

                    $("#statusRecipient").addClass("status-check");
                    $("#statusSignature").addClass("status-active");
                    TextControl.esign.nextStep('collapseSignature');
                })
                .catch(function (error) {
                    showError(error, "The workflow or field assignments could not be saved.");
                });
        },

        saveWorkflow: function (envelopeId) {
            return saveWorkflow(envelopeId)
                .then(function (envelope) {
                    currentEnvelope = envelope;
                    updateRecipients(currentEnvelope.signers, envelopeId, "envelope");
                    TextControl.esign.showToast("Workflow saved.", "warning");
                    return envelope;
                })
                .catch(function (error) {
                    showError(error, "The workflow could not be saved.");
                });
        },

        confirmRecipientsLegacy: function (envelopeId) {
            ensureRecipientFieldAssignments(envelopeId)
                .then(function (assigned) {
                    if (!assigned) return;

                    $("#statusRecipient").addClass("status-check");
                    $("#statusSignature").addClass("status-active");
                    TextControl.esign.nextStep('collapseSignature');
                })
                .catch(function (error) {
                    showError(error, "The field assignments could not be loaded.");
                });
        },

        continueContractRecipient: function () {
            $("#statusContractNextStep").addClass("status-check");
            $("#statusRecipient").addClass("status-active");
            TextControl.esign.nextStep("collapseRecipient");
        },

        initializeContractPreview: function (contractId) {
            if (!contractId) {
                return;
            }

            request("/contract/summary/" + contractId, {
                method: "GET",
                wait: false
            }).then(function (summary) {
                currentContract = {
                    contract: {
                        contractID: summary.contractId,
                        name: summary.name
                    },
                    thumbnail: summary.thumbnailSvg
                };

                displayContractPreview(currentContract);
            }).catch(function (error) {
                showError(error, "The contract could not be loaded.");
            });
        },

        applyRecipientFieldAssignments: function () {
            if (!pendingFieldAssignment) return;

            var modalElement = document.getElementById("fieldAssignmentModal");
            var assignments = [].slice.call(modalElement.querySelectorAll("[data-field-assignment]")).map(function (select) {
                return {
                    fieldId: select.dataset.fieldId,
                    signerId: select.value
                };
            });

            if (assignments.some(function (assignment) { return !assignment.signerId; })) {
                TextControl.esign.showToast("Assign each field to a recipient.", "warning");
                return;
            }

            saveFieldAssignments(pendingFieldAssignment.envelopeId, assignments)
                .then(function () {
                    var modal = bootstrap.Modal.getInstance(modalElement);
                    if (modal) {
                        modal.hide();
                    }

                    pendingFieldAssignment.resolve(true);
                    pendingFieldAssignment = null;
                })
                .catch(function (error) {
                    showError(error, "The field assignments could not be saved.");
                });
        },

        dropHandler: function (ev) {
            ev.preventDefault();
            var file;

            if (ev.dataTransfer.items) {

                if (ev.dataTransfer.items[0].kind === 'file') {
                    file = ev.dataTransfer.items[0].getAsFile();
                }
            } else {
                // Use DataTransfer interface to access the file(s)
                file = ev.dataTransfer.files[0];
            }

            var data = new FormData();
            data.append(file.name, file);
            appendSigningCertificate(data);

            uploadDocument(data);
        },

        dragOverHandler: function (ev) {
            ev.preventDefault();
        },

        nextStep: function (collapse) {

            $("#processSteps .collapse").each(function () {
                var myCollapse = document.getElementById(this.id)
                var bsCollapse = new bootstrap.Collapse(myCollapse, {
                    toggle: false
                });

                bsCollapse.hide();
            });

            var myCollapse = document.getElementById(collapse);
            var bsCollapse = new bootstrap.Collapse(myCollapse, {
                toggle: false
            })

            bsCollapse.show();

            $(".status-check").parent().addClass("status-border-checked");
        },

        createNewTemplate: function (nameInputId) {
            var nameInput = document.getElementById(nameInputId);
            var documentName = nameInput ? nameInput.value.trim() : "";

            if (!documentName) {
                if (nameInput) {
                    nameInput.focus();
                    nameInput.classList.add("is-invalid");
                }
                TextControl.esign.showToast("Enter a document name.");
                return;
            }

            if (nameInput) {
                nameInput.classList.remove("is-invalid");
            }

            postJson("/template/createnew/", { name: documentName })
                .then(function (message) {
                    var modalElement = document.getElementById("newTemplateModal");
                    var modal = modalElement && window.bootstrap ? bootstrap.Modal.getInstance(modalElement) : null;
                    if (modal) {
                        modal.hide();
                    }

                    displayTemplatePreview(message);
                })
                .catch(function (error) {
                    showError(error, "The template could not be created.");
                });

        },

        renameTemplate: function (templateId, nameInputId) {
            var nameInput = document.getElementById(nameInputId);
            var documentName = nameInput ? nameInput.value.trim() : "";

            if (!documentName) {
                if (nameInput) {
                    nameInput.focus();
                    nameInput.classList.add("is-invalid");
                }
                TextControl.esign.showToast("Enter a document name.");
                return;
            }

            postJson("/template/rename/" + templateId, { name: documentName })
                .then(function () {
                    window.location.reload();
                })
                .catch(function (error) {
                    showError(error, "The template could not be renamed.");
                });
        },

        updateSigningCertificate: function (envelopeId, signingCertificateId) {
            postJson("/envelope/signing-certificate/" + envelopeId, {
                signingCertificateId: signingCertificateId
            }, false)
                .then(function () {
                    TextControl.esign.showToast("Signing certificate updated.");
                })
                .catch(function (error) {
                    showError(error, "The signing certificate could not be updated.");
                });
        }

    }

    function uuidv4() {
        return 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, function (c) {
            var r = Math.random() * 16 | 0, v = c == 'x' ? r : (r & 0x3 | 0x8);
            return v.toString(16);
        });
    }

    function getAutoFillFieldName(fieldType) {
        if (fieldType === "signer-name" || fieldType === "signer-email") {
            var signerId = sanitizeMergeFieldName($("#formOwner").val() || "");
            if (!signerId) {
                return fieldType === "signer-name" ? "signer_name" : "signer_email";
            }

            return "signer_" + signerId + "_" + (fieldType === "signer-name" ? "name" : "email");
        }

        switch (fieldType) {
            case "current-date":
                return "current_date";
            case "sender-name":
                return "sender_name";
            case "document-name":
                return "document_name";
            default:
                return sanitizeMergeFieldName(fieldType);
        }
    }

    function isAutoFillFieldName(name) {
        if (!name) return false;

        return name === "signer_name" ||
            name === "signer_email" ||
            name === "current_date" ||
            name === "current_datetime" ||
            name === "document_name" ||
            name === "envelope_id" ||
            name === "sender_name" ||
            name === "sent_date" ||
            /^signer_[a-zA-Z0-9_]+_(name|email)$/.test(name);
    }

    function sanitizeMergeFieldName(value) {
        return String(value || "")
            .replace(/[^a-zA-Z0-9]/g, "_")
            .replace(/^_+|_+$/g, "");
    }

    function getSelectedFormOwner() {
        return $("#formOwner").val() || "unassigned";
    }

    function getAvailableSigners() {
        if (currentEnvelope && Array.isArray(currentEnvelope.signers) && currentEnvelope.signers.length) {
            return currentEnvelope.signers.filter(function (signer) {
                return getRecipientRoleValue(signer) === 0;
            }).map(function (signer) {
                return {
                    id: signer.id,
                    name: signer.name || signer.email || signer.id,
                    email: signer.email || signer.name || signer.id
                };
            });
        }

        return [].slice.call(document.querySelectorAll("[data-esign-action='insert-text-frame'][data-signer-id]")).map(function (element) {
            return {
                id: element.dataset.signerId,
                name: element.dataset.signerName || element.dataset.signerId,
                email: (element.querySelector(".toolbox-item-label") || {}).textContent || element.dataset.signerName || element.dataset.signerId
            };
        });
    }

    function getSignatureAreaOptions() {
        var includeSignerName = document.getElementById("signatureAreaIncludeSignerName");
        var includeDate = document.getElementById("signatureAreaIncludeDate");
        var selectedSignerIds = [].slice.call(document.querySelectorAll("input[name='signatureAreaSignerIds']:checked")).map(function (checkbox) {
            return String(checkbox.value);
        });

        return {
            selectedSignerIds: selectedSignerIds,
            includeSignerName: !!(includeSignerName && includeSignerName.checked),
            includeDate: !!(includeDate && includeDate.checked)
        };
    }

    function setDefaultSignatureAreaSignerSelection(signers) {
        var signerCheckboxes = [].slice.call(document.querySelectorAll("input[name='signatureAreaSignerIds']"));
        var selectedOwner = getSelectedFormOwner();
        var selectedOwnerMatched = false;

        if (!signerCheckboxes.length) {
            return;
        }

        signerCheckboxes.forEach(function (checkbox) {
            var shouldCheck = selectedOwner
                ? String(checkbox.value) === String(selectedOwner)
                : false;

            checkbox.checked = shouldCheck;
            selectedOwnerMatched = selectedOwnerMatched || shouldCheck;
        });

        if (!selectedOwnerMatched && signerCheckboxes.length) {
            signerCheckboxes[0].checked = true;
        }
    }

    function resolveSignatureAreaSigners(selectedSignerIds) {
        var signers = getAvailableSigners();
        if (!signers.length) {
            return [];
        }

        if (!selectedSignerIds || !selectedSignerIds.length) {
            return [];
        }

        return signers.filter(function (signer) {
            return selectedSignerIds.indexOf(String(signer.id)) !== -1;
        });
    }

    function insertSignatureAreaTable(signers, options) {
        return canAddTable().then(function (canAdd) {
            if (!canAdd) {
                throw new Error("A signature area cannot be inserted at this location.");
            }

            var signaturePlan = buildSignatureAreaPlan(options);
            var rowsPerSigner = signaturePlan.length;

            return setCurrentInputFormattingStyle("[Normal]").then(function () {
                return setCurrentInputFontSize(12);
            }).then(function () {
                return getExistingTableIds().then(function (usedTableIds) {
                    var createSequence = Promise.resolve();
                    var signerTables = [];
                    var nextTableId = 10;

                    function reserveTableId() {
                        while (usedTableIds[nextTableId]) {
                            nextTableId += 1;
                        }

                        usedTableIds[nextTableId] = true;
                        return nextTableId;
                    }

                    signers.forEach(function (signer, signerIndex) {
                        createSequence = createSequence.then(function () {
                            var tableId = reserveTableId();

                            return addTable(rowsPerSigner, 1, tableId).then(function () {
                                signerTables.push({
                                    signer: signer,
                                    tableId: tableId
                                });

                                if (signerIndex < signers.length - 1) {
                                    return insertSelectionText("\r\n");
                                }

                                return Promise.resolve();
                            });
                        });
                    });

                    return createSequence.then(function () {
                        var populateSequence = Promise.resolve();

                        signerTables.forEach(function (signerTable) {
                            populateSequence = populateSequence.then(function () {
                                return getTableById(signerTable.tableId).then(function (table) {
                                    var tableSequence = Promise.resolve();
                                    var cellMapPromise = getTableCellMap(table, 1);
                                    var rowIndex = 1;

                                    signaturePlan.forEach(function (step) {
                                        var currentRow = rowIndex;
                                        tableSequence = tableSequence.then(function () {
                                            if (step.type === "text-form-field") {
                                                return insertTextFormFieldIntoCell(cellMapPromise, currentRow, 1, signerTable.signer.id, step.fieldKey, step.label);
                                            }

                                            if (step.type === "merge") {
                                                return insertMergeFieldIntoCell(cellMapPromise, currentRow, 1, signerTable.signer.id, step.fieldType, step.label);
                                            }

                                            if (step.type === "signature") {
                                                return insertSignatureFieldIntoCell(cellMapPromise, currentRow, 1, signerTable.signer);
                                            }

                                            return Promise.resolve();
                                        });

                                        rowIndex += 1;
                                    });

                                    return tableSequence.then(function () {
                                        return styleSignatureAreaTable(cellMapPromise, 1, signaturePlan);
                                    }).then(function () {
                                        return selectTableAndSetFontSize(table, 12);
                                    });
                                });
                            });
                        });

                        return populateSequence;
                    });
                });
            });
        });
    }

    function ensureRecipientFieldAssignments(envelopeId) {
        if (!envelopeId) {
            return Promise.resolve(true);
        }

        return request("/envelope/field-assignments/" + envelopeId, {
            method: "GET",
            wait: false
        }).then(function (state) {
            if (!state.needsAssignment) {
                return true;
            }

            var signingRecipients = currentEnvelope && currentEnvelope.signers
                ? currentEnvelope.signers.filter(function (signer) { return getRecipientRoleValue(signer) === 0; })
                : [];

            if (signingRecipients.length === 1) {
                return saveFieldAssignments(envelopeId, state.fields.map(function (field) {
                    return {
                        fieldId: field.fieldId,
                        signerId: signingRecipients[0].id
                    };
                }));
            }

            return showFieldAssignmentModal(envelopeId, state.fields);
        });
    }

    function saveFieldAssignments(envelopeId, assignments) {
        return postJson("/envelope/field-assignments/" + envelopeId, {
            assignments: assignments
        }, false).then(function () {
            return true;
        });
    }

    function showFieldAssignmentModal(envelopeId, fields) {
        return new Promise(function (resolve) {
            var modalElement = document.getElementById("fieldAssignmentModal");
            var fieldList = document.getElementById("fieldAssignmentList");

            if (!modalElement || !fieldList || !currentEnvelope) {
                resolve(true);
                return;
            }

            fieldList.innerHTML = "";
            fields.forEach(function (field) {
                var row = document.createElement("div");
                row.className = "field-assignment-row";

                var details = document.createElement("div");
                details.className = "field-assignment-details";
                details.innerHTML = "<strong>" + escapeHtml(field.label) + "</strong><span>" + escapeHtml(field.fieldType) + "</span>";

                var select = document.createElement("select");
                select.className = "form-select";
                select.dataset.fieldAssignment = "true";
                select.dataset.fieldId = field.fieldId;
                select.setAttribute("aria-label", "Assign " + field.label + " to recipient");

                select.appendChild(new Option("Select recipient", ""));
                currentEnvelope.signers.filter(function (signer) {
                    return getRecipientRoleValue(signer) === 0;
                }).forEach(function (signer) {
                    select.appendChild(new Option(signer.email || signer.name || signer.id, signer.id));
                });

                row.appendChild(details);
                row.appendChild(select);
                fieldList.appendChild(row);
            });

            pendingFieldAssignment = {
                envelopeId: envelopeId,
                resolve: resolve
            };

            modalElement.addEventListener("hidden.bs.modal", function handleHidden() {
                modalElement.removeEventListener("hidden.bs.modal", handleHidden);
                if (pendingFieldAssignment && pendingFieldAssignment.resolve === resolve) {
                    pendingFieldAssignment.resolve(false);
                    pendingFieldAssignment = null;
                }
            });

            new bootstrap.Modal(modalElement).show();
        });
    }

    function escapeHtml(value) {
        return String(value || "")
            .replace(/&/g, "&amp;")
            .replace(/</g, "&lt;")
            .replace(/>/g, "&gt;")
            .replace(/"/g, "&quot;")
            .replace(/'/g, "&#039;");
    }

    function showMessageModal(message, variant) {
        var modalElement = ensureMessageModal();
        var titleElement = modalElement.querySelector("[data-esign-message-title]");
        var textElement = modalElement.querySelector("[data-esign-message-text]");
        var iconElement = modalElement.querySelector("[data-esign-message-icon]");
        var confirmButton = modalElement.querySelector("[data-esign-message-close]");
        var title = "Notice";
        var iconClass = "bi-info-circle";
        var iconTone = "";
        var buttonClass = "btn-warning";

        if (variant === "danger") {
            title = "Something Went Wrong";
            iconClass = "bi-exclamation-triangle";
            iconTone = " text-danger";
            buttonClass = "btn-danger";
        }
        else if (variant === "warning") {
            title = "Please Check This";
            iconClass = "bi-exclamation-circle";
        }
        else if (variant === "success") {
            title = "Success";
            iconClass = "bi-check2-circle";
        }

        titleElement.textContent = title;
        textElement.textContent = message || "Something went wrong. Please try again.";
        iconElement.className = "app-modal-icon" + iconTone;
        iconElement.innerHTML = '<i class="bi ' + iconClass + '"></i>';
        confirmButton.className = "btn " + buttonClass;

        new bootstrap.Modal(modalElement).show();
    }

    function ensureMessageModal() {
        var modal = document.getElementById("esignMessageModal");
        if (modal) {
            return modal;
        }

        modal = document.createElement("div");
        modal.id = "esignMessageModal";
        modal.className = "modal fade";
        modal.tabIndex = -1;
        modal.setAttribute("aria-labelledby", "esignMessageModalLabel");
        modal.setAttribute("aria-hidden", "true");
        modal.innerHTML = [
            '<div class="modal-dialog modal-dialog-centered">',
            '<div class="modal-content app-modal-content">',
            '<div class="modal-header app-modal-header">',
            '<div class="app-modal-icon" data-esign-message-icon aria-hidden="true"><i class="bi bi-info-circle"></i></div>',
            '<h5 class="modal-title" id="esignMessageModalLabel" data-esign-message-title>Notice</h5>',
            '<p data-esign-message-text></p>',
            '<button type="button" class="btn-close" data-bs-dismiss="modal" aria-label="Close"></button>',
            '</div>',
            '<div class="modal-footer app-modal-footer app-modal-footer-center">',
            '<button type="button" class="btn btn-warning" data-bs-dismiss="modal" data-esign-message-close>OK</button>',
            '</div>',
            '</div>',
            '</div>'
        ].join("");

        document.body.appendChild(modal);
        return modal;
    }

    function displayContractPreview(object) {

        currentContract = object;

        $("#statusDocumentThumbnail").attr("src", "data:image/svg+xml;base64," + object.thumbnail);
        $("#statusDocumentInfo").text(object.contract.name);
        $("#contractUploadBox").hide();
        $("#contractPreviewBox").show();
        $("#contractNextStepRow").removeClass("d-none");
        $("#editContractButton").attr("data-contract-id", object.contract.contractID || "");
        $("#statusContractNextStep").addClass("status-active");

        $("#statusDocument").addClass("status-check");
    }

    function displayTemplatePreview(object) {
        $("#statusDocumentThumbnail").attr("src", "data:image/svg+xml;base64," + object.thumbnail);
        $("#statusDocumentInfo").text(object.template.name);
        $("#contractUploadBox").hide();
        $("#contractPreviewBox").show();
        $("#templateNextStepRow").removeClass("d-none");
        $("#statusTemplateNextStep").addClass("status-active");

        var templateId = object.template.templateID || object.template.templateId;
        $("#editTemplateButton").attr("data-template-id", templateId || "");

        $("#statusDocument").addClass("status-check");
    }

    function refreshTemplateSummary(templateId) {
        if (!templateId) {
            return;
        }

        request("/template/summary/" + templateId, {
            method: "GET",
            wait: false
        }).then(function (summary) {
            if (summary.thumbnailSvg && document.getElementById("templateThumbnail")) {
                document.getElementById("templateThumbnail").src = "data:image/svg+xml;base64," + summary.thumbnailSvg;
            }

            if (summary.name && document.getElementById("templateTitle")) {
                document.getElementById("templateTitle").textContent = summary.name;
            }

            if (summary.thumbnailSvg && document.getElementById("statusDocumentThumbnail")) {
                document.getElementById("statusDocumentThumbnail").src = "data:image/svg+xml;base64," + summary.thumbnailSvg;
            }

            if (summary.name && document.getElementById("statusDocumentInfo")) {
                document.getElementById("statusDocumentInfo").textContent = summary.name;
            }
        }).catch(function () {
            // Keep save flow non-blocking if the preview refresh cannot be loaded.
        });
    }

    function refreshContractSummary(contractId) {
        if (!contractId) {
            return;
        }

        request("/contract/summary/" + contractId, {
            method: "GET",
            wait: false
        }).then(function (summary) {
            if (summary.thumbnailSvg && document.getElementById("statusDocumentThumbnail")) {
                document.getElementById("statusDocumentThumbnail").src = "data:image/svg+xml;base64," + summary.thumbnailSvg;
            }

            if (summary.name && document.getElementById("statusDocumentInfo")) {
                document.getElementById("statusDocumentInfo").textContent = summary.name;
            }
        }).catch(function () {
            // Keep save flow non-blocking if the preview refresh cannot be loaded.
        });
    }

    function updateRecipients(recipients, envelopeId, type) {
        $("#listRecipients").empty();
        syncWorkflowModeFromEnvelope();
        renderWorkflowDesigner(recipients, envelopeId);

        recipients.forEach(function (signer) {
            var authBadge = signer.requireEmailOtp
                ? "<span class=\"badge rounded-pill recipient-auth-badge\"><i class=\"bi bi-envelope-check\" aria-hidden=\"true\"></i> E-mail OTP</span>"
                : "";
            var roleBadge = "<span class=\"badge rounded-pill recipient-role-badge\">" + getRecipientRole(signer) + "</span>";
            var orderBadge = isComplexWorkflow() ? "<span class=\"badge rounded-pill recipient-order-badge\">Order " + getRecipientOrder(signer) + "</span>" : "";
            $("#listRecipients").append("<div class=\"list-group-item list-group-item-action\" aria-current=\"true\"><div class=\"d-flex w-100 justify-content-between gap-2\" ><h5 class=\"mb-1\">" + signer.name + "</h5><a class=\"btn btn-sm btn-outline-danger\" onclick=\"TextControl.esign.removeRecipient('" + envelopeId + "','" + type + "','" + signer.email + "','" + signer.name + "');\">Remove</a></div ><p class=\"mb-1\">" + signer.email + "</p><div class=\"recipient-badge-row\">" + roleBadge + orderBadge + authBadge + "</div></div >");
        });

        if (recipients.length != 0) {
            $("#btnConfirmRecipients").removeClass("disabled");
        }
        else {
            $("#btnConfirmRecipients").addClass("disabled");
        }
    }

    function isComplexWorkflow() {
        var complex = document.getElementById("workflowModeComplex");
        return !!(complex && complex.checked);
    }

    function getWorkflowMode() {
        return isComplexWorkflow() ? 1 : 0;
    }

    function getRecipientRole(signer) {
        return getRecipientRoleName(signer.role);
    }

    function getRecipientRoleValue(signer) {
        var role = signer && signer.role;
        if (typeof role === "number") return role;
        if (typeof role === "string") {
            var normalized = role.toLowerCase();
            if (normalized === "approver") return 1;
            if (normalized === "cc") return 2;
            if (normalized === "observer") return 3;
        }

        return 0;
    }

    function getRecipientRoleName(role) {
        var value = typeof role === "number" ? role : getRecipientRoleValue({ role: role });
        switch (value) {
            case 1: return "Approver";
            case 2: return "CC";
            case 3: return "Observer";
            default: return "Signer";
        }
    }

    function getRecipientOrder(signer) {
        return Math.max(parseInt(signer.routingOrder || "1", 10) || 1, 1);
    }

    function saveWorkflow(envelopeId) {
        if (!currentEnvelope || !Array.isArray(currentEnvelope.signers)) {
            return Promise.resolve(currentEnvelope);
        }

        var rows = [].slice.call(document.querySelectorAll("[data-workflow-recipient-id]"));
        var rowById = {};
        rows.forEach(function (row) {
            rowById[row.dataset.workflowRecipientId] = row;
        });

        var recipients = currentEnvelope.signers.map(function (signer) {
            var row = rowById[signer.id];
            var role = getWorkflowMode() === 1 && row
                ? parseInt((row.querySelector("[data-workflow-role]") || {}).value || "0", 10) || 0
                : 0;
            var order = role === 3
                ? 0
                : getWorkflowMode() === 1 && row
                ? Math.max(parseInt((row.querySelector("[data-workflow-order]") || {}).value || "1", 10) || 1, 1)
                : 1;
            var otpInput = row ? row.querySelector("[data-workflow-otp]") : null;

            return {
                id: signer.id,
                role: role,
                routingOrder: order,
                requireEmailOtp: otpInput ? !!otpInput.checked : !!signer.requireEmailOtp
            };
        });

        return postJson("/envelope/workflow/" + envelopeId, {
            workflowMode: getWorkflowMode(),
            recipients: recipients
        }, false);
    }

    function syncWorkflowModeFromEnvelope() {
        if (!currentEnvelope) return;
        var mode = currentEnvelope.workflowMode || 0;
        var simple = document.getElementById("workflowModeSimple");
        var complex = document.getElementById("workflowModeComplex");
        if ((mode === 0 || mode === "Simple") && complex && complex.checked) {
            updateWorkflowDesignerVisibility();
            return;
        }
        if (simple) simple.checked = mode !== 1 && mode !== "Complex";
        if (complex) complex.checked = mode === 1 || mode === "Complex";
        updateWorkflowDesignerVisibility();
    }

    function updateWorkflowDesignerVisibility() {
        var complex = isComplexWorkflow();
        var recipientOptions = document.getElementById("workflowRecipientOptions");
        var designer = document.getElementById("workflowDesigner");
        if (recipientOptions) recipientOptions.classList.toggle("d-none", !complex);
        if (designer) designer.classList.toggle("d-none", !complex);
    }

    function renderWorkflowDesigner(recipients, envelopeId) {
        updateWorkflowDesignerVisibility();
        var container = document.getElementById("workflowDesignerRows");
        if (!container) return;

        container.innerHTML = "";
        (recipients || []).forEach(function (signer) {
            var row = document.createElement("div");
            row.className = "workflow-designer-row";
            row.dataset.workflowRecipientId = signer.id;
            row.innerHTML =
                "<div class=\"workflow-designer-recipient\"><strong>" + escapeHtml(signer.name || signer.email || "") + "</strong><span>" + escapeHtml(signer.email || "") + "</span></div>" +
                "<select class=\"form-select form-select-sm\" data-workflow-role>" +
                buildRoleOption(0, "Signer", signer) +
                buildRoleOption(1, "Approver", signer) +
                buildRoleOption(2, "CC", signer) +
                buildRoleOption(3, "Observer", signer) +
                "</select>" +
                buildOrderControl(signer) +
                "<label class=\"workflow-designer-otp\"><input type=\"checkbox\" data-workflow-otp" + (signer.requireEmailOtp ? " checked" : "") + "> OTP</label>";
            container.appendChild(row);

            var roleSelect = row.querySelector("[data-workflow-role]");
            if (roleSelect) {
                roleSelect.addEventListener("change", function () {
                    signer.role = parseInt(roleSelect.value || "0", 10) || 0;
                    signer.routingOrder = signer.role === 3 ? 0 : getRecipientOrder(signer);
                    renderWorkflowDesigner(recipients, envelopeId);
                });
            }
        });
    }

    function buildRoleOption(role, label, signer) {
        return "<option value=\"" + role + "\"" + (getRecipientRoleValue(signer) === role ? " selected" : "") + ">" + label + "</option>";
    }

    function buildOrderControl(signer) {
        if (getRecipientRoleValue(signer) === 3) {
            return "<span class=\"workflow-no-order\" data-workflow-order-display>No order</span><input type=\"hidden\" value=\"0\" data-workflow-order>";
        }

        return "<input class=\"form-control form-control-sm\" type=\"number\" min=\"1\" step=\"1\" value=\"" + getRecipientOrder(signer) + "\" data-workflow-order>";
    }

    function escapeHtml(value) {
        return String(value || "")
            .replace(/&/g, "&amp;")
            .replace(/</g, "&lt;")
            .replace(/>/g, "&gt;")
            .replace(/"/g, "&quot;");
    }

    function uploadDocument(data) {
        request("/envelope/new", {
            method: "POST",
            body: data
        })
            .then(function (message) {
                window.location.href = "/envelopes/create/" + message;
            })
            .catch(function (error) {
                showError(error, "The document could not be uploaded.");
            });
    }

    function uploadContract(data) {
        request("/contract/new", {
            method: "POST",
            body: data
        })
            .then(function (message) {
                displayContractPreview(message);
            })
            .catch(function (error) {
                showError(error, "The contract could not be uploaded.");
            });
    }

    function uploadTemplate(data) {
        request("/template/new", {
            method: "POST",
            body: data
        })
            .then(function (message) {
                displayTemplatePreview(message);
            })
            .catch(function (error) {
                showError(error, "The template could not be uploaded.");
            });
    }

    function appendSigningCertificate(data) {
        var selector = document.getElementById("signingCertificateId");
        if (selector && selector.value) {
            data.append("signingCertificateId", selector.value);
        }
    }

    function canAddTable() {
        return new Promise(function (resolve) {
            if (!TXTextControl.tables || typeof TXTextControl.tables.getCanAdd !== "function") {
                resolve(false);
                return;
            }

            TXTextControl.tables.getCanAdd(function (canAdd) {
                resolve(canAdd !== false);
            });
        });
    }

    function addTable(rows, columns, tableId) {
        return new Promise(function (resolve, reject) {
            try {
                TXTextControl.tables.add(rows, columns, tableId, function (added) {
                    if (added === false) {
                        reject(new Error("The signature area table could not be created."));
                        return;
                    }

                    window.setTimeout(function () {
                        getTableById(tableId).then(resolve).catch(reject);
                    }, 0);
                }, function (error) {
                    reject(error);
                });
            }
            catch (error) {
                reject(error);
            }
        });
    }

    function getTableById(tableId) {
        return new Promise(function (resolve, reject) {
            try {
                TXTextControl.tables.getItem(function (table) {
                    if (!table) {
                        reject(new Error("The signature area table could not be accessed."));
                        return;
                    }

                    resolve(table);
                }, function (error) {
                    reject(error);
                }, tableId);
            }
            catch (error) {
                reject(error);
            }
        });
    }

    function getTableCellMap(table, columns) {
        return new Promise(function (resolve, reject) {
            table.cells.getCount(function (count) {
                if (!count) {
                    resolve({
                        columns: columns,
                        cells: []
                    });
                    return;
                }

                var remaining = count;
                var cells = [];
                var failed = false;

                table.cells.forEach(function (cell) {
                    if (failed) {
                        return;
                    }

                    cells.push(cell);
                    remaining -= 1;

                    if (remaining === 0) {
                        resolve({
                            columns: columns,
                            cells: cells
                        });
                    }
                }, function (error) {
                    if (failed) return;
                    failed = true;
                    reject(error);
                });
            }, reject);
        });
    }

    function getTableCell(cellMapPromise, row, column) {
        return Promise.resolve(cellMapPromise).then(function (cellMap) {
            var columnCount = cellMap.columns || 1;
            var index = ((row - 1) * columnCount) + (column - 1);
            var cell = cellMap.cells[index];
            if (!cell) {
                throw new Error("The signature area cell could not be accessed.");
            }

            return cell;
        });
    }

    function setTableCellText(cellMapPromise, row, column, text) {
        return getTableCell(cellMapPromise, row, column).then(function (cell) {
            return new Promise(function (resolve, reject) {
                var completed = false;

                function finish() {
                    if (completed) {
                        return;
                    }

                    completed = true;
                    resolve();
                }

                try {
                    cell.setText(text, function () {
                        finish();
                    });

                    window.setTimeout(finish, 0);
                }
                catch (error) {
                    reject(error);
                }
            });
        });
    }

    function getTableCellFormat(cell) {
        return new Promise(function (resolve, reject) {
            try {
                cell.getCellFormat(function (format) {
                    resolve(format);
                }, function (error) {
                    reject(error);
                });
            }
            catch (error) {
                reject(error);
            }
        });
    }

    function applyBorderStyle(border, width, color, textDistance) {
        return new Promise(function (resolve, reject) {
            try {
                border.setWidth(width, function () {
                    border.setColor(color, function () {
                        border.setTextDistance(textDistance, function () {
                            resolve();
                        }, function (error) {
                            reject(error);
                        });
                    }, function (error) {
                        reject(error);
                    });
                }, function (error) {
                    reject(error);
                });
            }
            catch (error) {
                reject(error);
            }
        });
    }

    function formatTableCell(cellMapPromise, row, column, options) {
        return getTableCell(cellMapPromise, row, column).then(function (cell) {
            return getTableCellFormat(cell).then(function (format) {
                var verticalPadding = options && typeof options.verticalPadding === "number" ? options.verticalPadding : 70;
                var horizontalPadding = options && typeof options.horizontalPadding === "number" ? options.horizontalPadding : 90;
                var topBorder = options && options.topBorder ? options.topBorder : { width: 0, color: "#d7deea", textDistance: verticalPadding };
                var bottomBorder = options && options.bottomBorder ? options.bottomBorder : { width: 0, color: "#d7deea", textDistance: verticalPadding };
                var leftBorder = options && options.leftBorder ? options.leftBorder : { width: 0, color: "#d7deea", textDistance: horizontalPadding };
                var rightBorder = options && options.rightBorder ? options.rightBorder : { width: 0, color: "#d7deea", textDistance: horizontalPadding };

                return Promise.all([
                    applyBorderStyle(format.topBorder, topBorder.width, topBorder.color, topBorder.textDistance || verticalPadding),
                    applyBorderStyle(format.bottomBorder, bottomBorder.width, bottomBorder.color, bottomBorder.textDistance || verticalPadding),
                    applyBorderStyle(format.leftBorder, leftBorder.width, leftBorder.color, leftBorder.textDistance || horizontalPadding),
                    applyBorderStyle(format.rightBorder, rightBorder.width, rightBorder.color, rightBorder.textDistance || horizontalPadding)
                ]);
            });
        });
    }

    function styleSignatureAreaTable(cellMapPromise, signerCount, signaturePlan) {
        var formattingTasks = [];
        var rowsPerSigner = signaturePlan.length;
        var totalRows = signerCount * rowsPerSigner;
        var outerBorderColor = "#94a3b8";
        var signatureBorderColor = "#64748b";
        var outerBorderWidth = 30;
        var signatureBorderWidth = 36;

        for (var signerIndex = 0; signerIndex < signerCount; signerIndex++) {
            for (var stepIndex = 0; stepIndex < signaturePlan.length; stepIndex++) {
                var currentRow = (signerIndex * rowsPerSigner) + stepIndex + 1;
                var step = signaturePlan[stepIndex];
                var isSignatureRow = step && step.type === "signature";
                var isFirstRow = currentRow === 1;
                var isLastRow = currentRow === totalRows;

                formattingTasks.push(formatTableCell(cellMapPromise, currentRow, 1, {
                    verticalPadding: isSignatureRow ? 110 : 70,
                    horizontalPadding: isSignatureRow ? 120 : 90,
                    topBorder: {
                        width: isFirstRow ? outerBorderWidth : 0,
                        color: outerBorderColor
                    },
                    bottomBorder: {
                        width: isSignatureRow ? signatureBorderWidth : (isLastRow ? outerBorderWidth : 0),
                        color: isSignatureRow ? signatureBorderColor : outerBorderColor
                    },
                    leftBorder: {
                        width: outerBorderWidth,
                        color: outerBorderColor
                    },
                    rightBorder: {
                        width: outerBorderWidth,
                        color: outerBorderColor
                    }
                }));
            }
        }

        return Promise.all(formattingTasks);
    }

    function setSelectionToCellStart(cellMapPromise, row, column) {
        return getTableCell(cellMapPromise, row, column).then(function (cell) {
            return new Promise(function (resolve, reject) {
                try {
                    cell.getStart(function (start) {
                        TXTextControl.selection.setStart(Math.max((start || 0) - 1, 0), function () {
                            TXTextControl.selection.setLength(0);
                            resolve();
                        });
                    });
                }
                catch (error) {
                    reject(error);
                }
            });
        });
    }

    function insertMergeFieldIntoCell(cellMapPromise, row, column, signerId, fieldType, label) {
        return setSelectionToCellStart(cellMapPromise, row, column).then(function () {
            return withTemporaryFormOwner(signerId, function () {
                TextControl.esign.insertAutoFillField(fieldType, label);
                return waitForEditorTick();
            });
        });
    }

    function insertTextFormFieldIntoCell(cellMapPromise, row, column, signerId, fieldKey, label) {
        return setSelectionToCellStart(cellMapPromise, row, column).then(function () {
            return insertSelectionText((label || "") + "\r\n").then(function () {
                return collapseSelectionToEnd();
            }).then(function () {
                return new Promise(function (resolve, reject) {
                    try {
                        TXTextControl.formFields.getCanAdd(function (canAdd) {
                            if (!canAdd) {
                                reject(new Error("The signature area field could not be inserted at this location."));
                                return;
                            }

                            TXTextControl.formFields.addTextFormField(3000, function (ff) {
                                ff.setName(String(signerId || "unassigned") + ":" + String(fieldKey || "field") + ":" + uuidv4());
                                resolve();
                            });
                        }, function (error) {
                            reject(error);
                        });
                    }
                    catch (error) {
                        reject(error);
                    }
                }).then(waitForEditorTick);
            });
        });
    }

    function insertSelectionText(text) {
        return new Promise(function (resolve, reject) {
            try {
                TXTextControl.selection.setText(text, function () {
                    resolve();
                }, function (error) {
                    reject(error);
                });
            }
            catch (error) {
                reject(error);
            }
        });
    }

    function collapseSelectionToEnd() {
        return new Promise(function (resolve, reject) {
            try {
                TXTextControl.selection.getStart(function (start) {
                    TXTextControl.selection.getLength(function (length) {
                        TXTextControl.selection.setStart((start || 0) + (length || 0), function () {
                            resolve();
                        });
                    }, function (error) {
                        reject(error);
                    });
                }, function (error) {
                    reject(error);
                });
            }
            catch (error) {
                reject(error);
            }
        });
    }

    function setCurrentInputFontSize(fontSize) {
        return new Promise(function (resolve, reject) {
            try {
                TXTextControl.selection.setFontSize(fontSize, function () {
                    resolve();
                }, function (error) {
                    reject(error);
                });
            }
            catch (error) {
                reject(error);
            }
        });
    }

    function setCurrentInputFormattingStyle(styleName) {
        return new Promise(function (resolve, reject) {
            try {
                TXTextControl.selection.setFormattingStyle(styleName, function () {
                    resolve();
                }, function (error) {
                    reject(error);
                });
            }
            catch (error) {
                reject(error);
            }
        });
    }

    function insertSignatureFieldIntoCell(cellMapPromise, row, column, signer) {
        return setSelectionToCellStart(cellMapPromise, row, column).then(function () {
            return new Promise(function (resolve, reject) {
                try {
                    TXTextControl.selection.getStart(function (start) {
                        var fieldName = signer && signer.id ? "txsign_" + signer.id : "txsign_unassigned:" + uuidv4();
                        var fieldSize = { width: 2600, height: 900 };

                        function finalizeSignatureField(addedTextFrame) {
                            addedTextFrame.setName(fieldName);
                            TextControl.esign.checkTextFrames();
                            resolve();
                        }

                        if (TXTextControl.signatureFields && typeof TXTextControl.signatureFields.addInline === "function") {
                            TXTextControl.signatureFields.addInline(
                                fieldSize,
                                start,
                                finalizeSignatureField,
                                function (error) {
                                    reject(error);
                                }
                            );

                            return;
                        }

                        TXTextControl.signatureFields.addAnchored(
                            fieldSize,
                            TXTextControl.HorizontalAlignment.Left,
                            start,
                            TXTextControl.TextFrameInsertionMode.AboveTheText,
                            finalizeSignatureField,
                            function (error) {
                                reject(error);
                            }
                        );
                    });
                }
                catch (error) {
                    reject(error);
                }
            });
        });
    }

    function withTemporaryFormOwner(ownerId, callback) {
        var ownerSelect = document.getElementById("formOwner");
        var previousValue = ownerSelect ? ownerSelect.value : null;

        if (ownerSelect && ownerId) {
            ownerSelect.value = String(ownerId);
        }

        return Promise.resolve()
            .then(callback)
            .finally(function () {
                if (ownerSelect && previousValue !== null) {
                    ownerSelect.value = previousValue;
                }
            });
    }

    function waitForEditorTick() {
        return new Promise(function (resolve) {
            window.setTimeout(resolve, 0);
        });
    }

    function buildSignatureAreaPlan(options) {
        var plan = [
            {
                type: "text-form-field",
                fieldKey: "company",
                label: "Company"
            },
            {
                type: "signature"
            },
            {
                type: "text-form-field",
                fieldKey: "title",
                label: "Title"
            }
        ];

        if (!options || options.includeDate) {
            plan.splice(2, 0, {
                type: "merge",
                fieldType: "current-date",
                label: "Current Date"
            });
        }

        if (!options || options.includeSignerName) {
            plan.splice(plan.length - 1, 0, {
                type: "merge",
                fieldType: "signer-name",
                label: "Signer Name"
            });
        }

        return plan;
    }

    function selectTableAndSetFontSize(table, fontSize) {
        return new Promise(function (resolve, reject) {
            try {
                table.select(function () {
                    TXTextControl.selection.setFontSize(fontSize, function () {
                        resolve();
                    }, function (error) {
                        reject(error);
                    });
                }, function (error) {
                    reject(error);
                });
            }
            catch (error) {
                reject(error);
            }
        });
    }

    function getExistingTableIds() {
        return new Promise(function (resolve) {
            if (!TXTextControl.tables || typeof TXTextControl.tables.getCount !== "function" || typeof TXTextControl.tables.forEach !== "function") {
                resolve({});
                return;
            }

            var usedIds = {};

            try {
                TXTextControl.tables.getCount(function (count) {
                    if (!count) {
                        resolve(usedIds);
                        return;
                    }

                    var remaining = count;
                    var failed = false;

                    TXTextControl.tables.forEach(function (table) {
                        if (failed) {
                            return;
                        }

                        if (!table || typeof table.getID !== "function") {
                            remaining -= 1;
                            if (remaining === 0) {
                                resolve(usedIds);
                            }
                            return;
                        }

                        table.getID(function (id) {
                            if (typeof id === "number" && id >= 10) {
                                usedIds[id] = true;
                            }

                            remaining -= 1;
                            if (remaining === 0) {
                                resolve(usedIds);
                            }
                        }, function () {
                            remaining -= 1;
                            if (remaining === 0) {
                                resolve(usedIds);
                            }
                        });
                    }, function () {
                        if (failed) return;
                        failed = true;
                        resolve(usedIds);
                    });
                }, function () {
                    resolve(usedIds);
                });
            }
            catch (_) {
                resolve(usedIds);
            }
        });
    }

    function showForegroundModal(modalElement) {
        if (!modalElement) {
            return null;
        }

        if (modalElement.parentElement !== document.body) {
            document.body.appendChild(modalElement);
        }

        modalElement.classList.add("editor-surface-modal");

        modalElement.addEventListener("shown.bs.modal", function handleShown() {
            modalElement.removeEventListener("shown.bs.modal", handleShown);

            var backdrop = document.querySelector(".modal-backdrop:last-of-type");
            if (backdrop) {
                backdrop.classList.add("editor-surface-backdrop");
            }

            var focusTarget = modalElement.querySelector("input, button, select, textarea");
            if (focusTarget) {
                focusTarget.focus();
            }
        });

        var modal = bootstrap.Modal.getInstance(modalElement) || new bootstrap.Modal(modalElement);
        modal.show();
        return modal;
    }

    document.addEventListener("DOMContentLoaded", function () {
        var uploadBox = document.getElementById("uploadbox");
        var files = document.getElementById("files");

        if (uploadBox && files) {
            uploadBox.addEventListener("click", function () {
                files.click();
            });
        }

        document.querySelectorAll(".prevent").forEach(function (element) {
            element.addEventListener("click", function (event) {
                event.stopPropagation();
            });
        });

        var processForm = document.getElementById("processForm");

        if (processForm) {
            processForm.addEventListener("submit", function (event) {
                event.preventDefault();
                event.stopPropagation();
            });
        }

        var signingCertificate = document.getElementById("signingCertificateId");
        if (signingCertificate && signingCertificate.dataset.envelopeId) {
            signingCertificate.addEventListener("change", function () {
                TextControl.esign.updateSigningCertificate(signingCertificate.dataset.envelopeId, signingCertificate.value);
            });
        }

        document.querySelectorAll("input[name='workflowMode']").forEach(function (input) {
            input.addEventListener("change", function () {
                updateWorkflowDesignerVisibility();
                if (currentEnvelope && currentEnvelope.signers) {
                    renderWorkflowDesigner(currentEnvelope.signers, (document.querySelector("[data-envelope-id]") || {}).dataset.envelopeId);
                }
            });
        });
    });

    return tx;

}(TextControl || {}));
