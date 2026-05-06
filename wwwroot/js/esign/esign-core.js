var TextControl = (function (tx) {

    var currentEnvelope;
    var currentContract;

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
        TextControl.esign.showToast(message || "Something went wrong. Please try again.");
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

        insertTextFrame: function (id, name) {
            TXTextControl.selection.getStart(function (start) {
                TXTextControl.signatureFields.addAnchored(
                    { width: 4000, height: 2000 },
                    TXTextControl.HorizontalAlignment.Left,
                    start, // TextPosition
                    TXTextControl.TextFrameInsertionMode.AboveTheText,

                    (addedTextFrame) => {
                        addedTextFrame.setName("txsign_" + id);
                        TextControl.esign.checkTextFrames();
                    }
                );
            });
        },

        checkTextFrames: function () {

            $(".toolbox-item-small").removeClass("checked");

            TXTextControl.textFrames.forEach(function (frame) {
                frame.getName(function (name) {
                    $("#" + name).addClass("checked");
                });
            });

        },

        insertTextFormField: function () {
            TXTextControl.formFields.getCanAdd(canAdd => {
                if (canAdd) {

                    var formOwner = $("#formOwner").val();

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

                    var formOwner = $("#formOwner").val();

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

                    var formOwner = $("#formOwner").val();

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

                    var formOwner = $("#formOwner").val();

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

            TextControl.esign.showToast("Saving...");

            postJson("/template/saveDocument/" + envelopeId, signModel, false)
                .then(function () {
                    TextControl.esign.showToast("Document successfully saved!");
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

        submitEnvelope: function (envelopeId) {
            postJson("/envelope/submit/" + envelopeId)
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

        getApplicationFields: function (templateId) {
            postJson("/template/getfields/" + templateId)
                .then(function (status) {

                    $("#tx-fields").empty();

                    $("#tx-fields").append("<form id='submitfields' method='post' action='/template/instance/" + templateId + "' >");

                    if (status.length === 0) {
                        $("#submitfields").append("<p>No merge fields found.</p>");
                    }
                    else {

                        status.forEach(function (field) {
                            $("#submitfields").append("<div class='mt-2'><label for='" + field.name + "' class='form-label'>" + field.name + "</label><input class='form-control' type='text' placeholder='Complete this field' name='" + field.name + "' id='" + field.name + "' /></div>");
                        });

                    }

                    $("#submitfields").append("<input value='Create Instance' class='mt-5 btn btn-warning' type='submit'>");
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
                $("#reviewRecipient").append("<li>" + signer.email + "</li>")
            });

        },

        showToast: function (statusText, variant) {
            $("#liveToastMessage").text(statusText);
            var myToast = document.getElementById("liveToast");
            if (myToast) {
                myToast.classList.remove("bg-success", "bg-danger", "bg-warning", "text-white", "text-dark");
                if (variant === "danger") {
                    myToast.classList.add("bg-danger", "text-white");
                }
                else if (variant === "warning") {
                    myToast.classList.add("bg-warning", "text-dark");
                }
                else {
                    myToast.classList.add("bg-success", "text-white");
                }
            }
            var toast = new bootstrap.Toast(myToast);
            toast.show();
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

                        var data = { "name": name, "email": email };

                        postJson(url, data, false)
                            .then(function (envelope) {
                                currentEnvelope = envelope;
                                TextControl.esign.showToast("Recipient successfully updated!");

                                if (type === "envelope") {
                                    updateRecipients(currentEnvelope.signers, envelopeId, type);

                                    $("#recipientAlreadyAdded").addClass("collapse");

                                    $("#signerName").val("");
                                    $("#signerEmail").val("");
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

        confirmRecipients: function () {
            $("#statusRecipient").addClass("status-check");
            $("#statusSignature").addClass("status-active");
            TextControl.esign.nextStep('collapseSignature');
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
        }

    }

    function uuidv4() {
        return 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, function (c) {
            var r = Math.random() * 16 | 0, v = c == 'x' ? r : (r & 0x3 | 0x8);
            return v.toString(16);
        });
    }

    function displayContractPreview(object) {

        currentContract = object;

        $("#statusDocumentThumbnail").attr("src", "data:image/svg+xml;base64," + object.thumbnail);
        $("#statusDocumentInfo").text(object.contract.name);
        $("#contractUploadBox").hide();
        $("#contractPreviewBox").show();

        $("#statusDocument").addClass("status-check");
        $("#statusRecipient").addClass("status-active");
        TextControl.esign.showToast("Document successfully uploaded!");
        TextControl.esign.nextStep("collapseRecipient");
    }

    function displayTemplatePreview(object) {
        $("#statusDocumentThumbnail").attr("src", "data:image/svg+xml;base64," + object.thumbnail);
        $("#statusDocumentInfo").text(object.template.name);
        $("#contractUploadBox").hide();
        $("#contractPreviewBox").show();

        $("#statusDocument").addClass("status-check");
        TextControl.esign.showToast("Template successfully created!");
    }

    function updateRecipients(recipients, envelopeId, type) {
        $("#listRecipients").empty();

        recipients.forEach(function (signer) {
            $("#listRecipients").append("<div class=\"list-group-item list-group-item-action\" aria-current=\"true\"><div class=\"d-flex w-100 justify-content-between\" ><h5 class=\"mb-1\">" + signer.name + "</h5><a class=\"btn btn-sm btn-outline-danger\" onclick=\"TextControl.esign.removeRecipient('" + envelopeId + "','" + type + "','" + signer.email + "','" + signer.name + "');\">Remove</a></div ><p class=\"mb-1\">" + signer.email + "</p></div >");
        });

        if (recipients.length != 0) {
            $("#btnConfirmRecipients").removeClass("disabled");
        }
        else {
            $("#btnConfirmRecipients").addClass("disabled");
        }
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
    });

    return tx;

}(TextControl || {}));
