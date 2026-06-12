(function () {
    document.addEventListener("DOMContentLoaded", function () {
        initThemeToggle(document);
        initTree(document);
        initEnhancedTables(document);

        new MutationObserver(function (mutations) {
            mutations.forEach(function (mutation) {
                mutation.addedNodes.forEach(function (node) {
                    if (node.nodeType === 1) {
                        initThemeToggle(node);
                        initTree(node);
                        initEnhancedTables(node);
                    }
                });
            });
        }).observe(document.body, { childList: true, subtree: true });

        document.addEventListener("esign:partial-loaded", function (event) {
            initThemeToggle(event.target);
            initTree(event.target);
            initEnhancedTables(event.target);
        });
    });

    function getStoredTheme() {
        return localStorage.getItem("signfabric-theme");
    }

    function getPreferredTheme() {
        var storedTheme = getStoredTheme();
        if (storedTheme) return storedTheme;

        return window.matchMedia && window.matchMedia("(prefers-color-scheme: dark)").matches
            ? "dark"
            : "light";
    }

    function applyTheme(theme) {
        document.documentElement.setAttribute("data-theme", theme);
        document.documentElement.setAttribute("data-bs-theme", theme);

        [].slice.call(document.querySelectorAll("[data-theme-toggle]")).forEach(function (toggle) {
            var isDark = theme === "dark";
            var icon = toggle.querySelector(".bi");
            var label = toggle.querySelector(".theme-toggle-label");

            toggle.setAttribute("aria-pressed", isDark ? "true" : "false");
            toggle.setAttribute("aria-label", isDark ? "Switch to light mode" : "Switch to dark mode");

            if (icon) {
                icon.className = isDark ? "bi bi-sun" : "bi bi-moon-stars";
            }

            if (label) {
                label.textContent = isDark ? "Light" : "Dark";
            }
        });
    }

    function initThemeToggle(root) {
        applyTheme(getPreferredTheme());

        var toggles = root.matches && root.matches("[data-theme-toggle]")
            ? [root]
            : [].slice.call(root.querySelectorAll ? root.querySelectorAll("[data-theme-toggle]") : []);

        toggles.forEach(function (toggle) {
            if (toggle.dataset.themeToggleInitialized === "true") return;
            toggle.dataset.themeToggleInitialized = "true";

            toggle.addEventListener("click", function () {
                var currentTheme = document.documentElement.getAttribute("data-theme") || getPreferredTheme();
                var nextTheme = currentTheme === "dark" ? "light" : "dark";
                localStorage.setItem("signfabric-theme", nextTheme);
                applyTheme(nextTheme);
            });
        });
    }

    function initEnhancedTables(root) {
        var tables = root.matches && root.matches("[data-esign-table='overview']")
            ? [root]
            : [].slice.call(root.querySelectorAll ? root.querySelectorAll("[data-esign-table='overview']") : []);

        tables.forEach(initEnhancedTable);
    }

    function initEnhancedTable(table) {
        if (table.dataset.esignTableInitialized === "true") return;

        var tbody = table.tBodies[0];
        if (!tbody) return;

        table.dataset.esignTableInitialized = "true";

        var pageSizeOptions = [10, 20, 50, 100];
        var pageSize = parseInt(table.dataset.pageSize || "10", 10);
        if (pageSizeOptions.indexOf(pageSize) === -1) pageSize = 10;
        var rows = [].slice.call(tbody.rows);
        var state = {
            page: 1,
            filter: "",
            sortColumn: -1,
            sortDirection: "asc"
        };

        var toolbar = document.createElement("div");
        toolbar.className = "table-tools";
        toolbar.innerHTML = [
            '<div class="table-tools-search">',
            '<i class="bi bi-search" aria-hidden="true"></i>',
            '<input type="search" class="form-control" placeholder="Filter table" aria-label="Filter table" />',
            '</div>',
            '<div class="table-tools-end">',
            '<label class="table-page-size">Rows',
            '<select class="form-select form-select-sm" aria-label="Rows per page">',
            pageSizeOptions.map(function (option) {
                return '<option value="' + option + '"' + (option === pageSize ? ' selected' : '') + '>' + option + '</option>';
            }).join(""),
            '</select>',
            '</label>',
            '<div class="table-tools-meta" aria-live="polite"></div>',
            '</div>'
        ].join("");

        table.parentNode.insertBefore(toolbar, table);

        var filterInput = toolbar.querySelector("input");
        var pageSizeSelect = toolbar.querySelector("select");
        var meta = toolbar.querySelector(".table-tools-meta");
        var pager = document.createElement("div");
        pager.className = "table-pager";
        table.parentNode.insertBefore(pager, table.nextSibling);

        var emptyRow = document.createElement("tr");
        emptyRow.className = "table-empty-row";
        var emptyCell = document.createElement("td");
        emptyCell.colSpan = table.tHead && table.tHead.rows.length ? table.tHead.rows[0].cells.length : 1;
        emptyCell.textContent = "No matching records.";
        emptyRow.appendChild(emptyCell);

        [].slice.call(table.querySelectorAll("thead th")).forEach(function (header, index) {
            if (header.dataset.sortable === "false") return;

            header.classList.add("table-sortable");
            header.tabIndex = 0;
            header.setAttribute("role", "button");
            header.setAttribute("aria-sort", "none");

            var label = document.createElement("span");
            label.className = "table-sort-label";
            label.innerHTML = header.innerHTML;
            header.innerHTML = "";
            header.appendChild(label);

            var indicator = document.createElement("i");
            indicator.className = "bi bi-arrow-down-up table-sort-icon";
            indicator.setAttribute("aria-hidden", "true");
            header.appendChild(indicator);

            function toggleSort() {
                if (state.sortColumn === index) {
                    state.sortDirection = state.sortDirection === "asc" ? "desc" : "asc";
                }
                else {
                    state.sortColumn = index;
                    state.sortDirection = "asc";
                }

                state.page = 1;
                render();
            }

            header.addEventListener("click", toggleSort);
            header.addEventListener("keydown", function (event) {
                if (event.key === "Enter" || event.key === " ") {
                    event.preventDefault();
                    toggleSort();
                }
            });
        });

        filterInput.addEventListener("input", function () {
            state.filter = filterInput.value.trim().toLowerCase();
            state.page = 1;
            render();
        });

        pageSizeSelect.addEventListener("change", function () {
            pageSize = parseInt(pageSizeSelect.value, 10);
            state.page = 1;
            render();
        });

        function getCellValue(row, index) {
            var cell = row.cells[index];
            return cell ? cell.textContent.trim() : "";
        }

        function getSortValue(row, index) {
            var text = getCellValue(row, index);
            var timestamp = Date.parse(text);
            if (!Number.isNaN(timestamp)) return timestamp;

            var normalizedNumber = text.replace(",", ".").trim();
            if (/^-?\d+(\.\d+)?$/.test(normalizedNumber)) {
                return Number(normalizedNumber);
            }

            return text.toLowerCase();
        }

        function filteredRows() {
            var result = rows.filter(function (row) {
                return !state.filter || row.textContent.toLowerCase().indexOf(state.filter) !== -1;
            });

            if (state.sortColumn >= 0) {
                result.sort(function (a, b) {
                    var aValue = getSortValue(a, state.sortColumn);
                    var bValue = getSortValue(b, state.sortColumn);
                    var direction = state.sortDirection === "asc" ? 1 : -1;

                    if (aValue < bValue) return -1 * direction;
                    if (aValue > bValue) return 1 * direction;
                    return 0;
                });
            }

            return result;
        }

        function renderHeaders() {
            [].slice.call(table.querySelectorAll("thead th")).forEach(function (header, index) {
                if (!header.classList.contains("table-sortable")) return;

                var icon = header.querySelector(".table-sort-icon");
                header.setAttribute("aria-sort", "none");
                icon.className = "bi bi-arrow-down-up table-sort-icon";

                if (state.sortColumn === index) {
                    header.setAttribute("aria-sort", state.sortDirection === "asc" ? "ascending" : "descending");
                    icon.className = "bi " + (state.sortDirection === "asc" ? "bi-sort-alpha-down" : "bi-sort-alpha-up") + " table-sort-icon";
                }
            });
        }

        function renderPager(totalRows, totalPages) {
            pager.innerHTML = "";

            if (totalRows <= pageSize) return;

            var previous = document.createElement("button");
            previous.type = "button";
            previous.className = "btn btn-sm btn-outline-secondary";
            previous.textContent = "Previous";
            previous.disabled = state.page === 1;
            previous.addEventListener("click", function () {
                state.page -= 1;
                render();
            });

            var next = document.createElement("button");
            next.type = "button";
            next.className = "btn btn-sm btn-outline-secondary";
            next.textContent = "Next";
            next.disabled = state.page === totalPages;
            next.addEventListener("click", function () {
                state.page += 1;
                render();
            });

            var label = document.createElement("span");
            label.className = "table-pager-label";
            label.textContent = "Page " + state.page + " of " + totalPages;

            pager.appendChild(previous);
            pager.appendChild(label);
            pager.appendChild(next);
        }

        function render() {
            var result = filteredRows();
            var totalPages = Math.max(1, Math.ceil(result.length / pageSize));

            if (state.page > totalPages) state.page = totalPages;

            var start = (state.page - 1) * pageSize;
            var visible = result.slice(start, start + pageSize);

            tbody.innerHTML = "";
            visible.forEach(function (row) {
                tbody.appendChild(row);
            });

            if (result.length === 0) {
                tbody.appendChild(emptyRow);
            }

            var first = result.length === 0 ? 0 : start + 1;
            var last = Math.min(start + visible.length, result.length);
            meta.textContent = "Showing " + first + "-" + last + " of " + result.length;

            renderHeaders();
            renderPager(result.length, totalPages);
        }

        render();
    }

    function initTree(root) {
        var elements = root.matches && root.matches("[data-esign-init]")
            ? [root]
            : [].slice.call(root.querySelectorAll ? root.querySelectorAll("[data-esign-init]") : []);

        elements.forEach(initElement);
    }

    function initElement(element) {
        var init = element.dataset.esignInit;

        if (element.dataset.esignInitialized === "true") return;

        if (requiresEditor(init) && typeof TXTextControl === "undefined") {
            window.setTimeout(function () {
                initElement(element);
            }, 50);
            return;
        }

        element.dataset.esignInitialized = "true";

        if (init === "envelope-create") {
            var envelopeId = element.dataset.envelopeId;
            TextControl.esign.loadPartial("#collapseSignature", "/envelopes/edit/" + envelopeId + "?handler=SignatureBoxPartial");
            TextControl.esign.receiveRecipients(envelopeId, "envelope");
        }

        if (init === "template-details") {
            TextControl.esign.getApplicationFields(element.dataset.templateId, element.dataset.canRequestSignatures !== "false");
        }

        if (init === "contract-create") {
            TextControl.esign.initializeContractPreview(element.dataset.contractId);
        }

        if (init === "envelope-editor") {
            initEnvelopeEditor(element.dataset.envelopeId);
        }

        if (init === "template-editor") {
            initTemplateEditor(element.dataset.templateId);
        }

        if (init === "contract-editor") {
            initContractEditor(element.dataset.contractId);
        }

        if (init === "review-signing") {
            window.TextControlEsignPages.initReviewSigning();
        }

        if (init === "collaboration") {
            window.TextControlEsignPages.initCollaboration(element.dataset.documentId, element.dataset.owner === "true");
        }
    }

    function requiresEditor(init) {
        return init === "envelope-editor" ||
            init === "template-editor" ||
            init === "contract-editor" ||
            init === "collaboration";
    }

    function enableTooltips() {
        var tooltipTriggerList = [].slice.call(document.querySelectorAll('[data-bs-toggle="tooltip"]'));
        tooltipTriggerList.map(function (tooltipTriggerEl) {
            return new bootstrap.Tooltip(tooltipTriggerEl);
        });
    }

    function initRequiredFieldBehavior() {
        $("#fieldRequired").off("change.esign").on("change.esign", function () {
            TXTextControl.formFields.getItem(function (ff) {
                ff.setID(this.checked ? 1 : 0);
            }.bind(this));
        });

        TXTextControl.addEventListener("textFieldLeft", function () {
            $("#fieldProperties").addClass("d-none");
        });
    }

    function showEditorChrome() {
        TXTextControl.showVerticalRuler(true);
        TXTextControl.showHorizontalRuler(true);
        TXTextControl.showStatusBar(true);
    }

    function initSignatureFieldSidebarTracking() {
        TXTextControl.addEventListener("signatureFieldCreated", function () {
            TextControl.esign.checkTextFrames();
        });

        TXTextControl.addEventListener("signatureFieldDeleted", function () {
            TextControl.esign.checkTextFrames();
            TextControl.esign.clearActiveSignatureButton();
        });

        TXTextControl.addEventListener("signatureFieldSelected", function () {
            TXTextControl.signatureFields.getItem(function (signatureField) {
                if (!signatureField || typeof signatureField.getName !== "function") {
                    TextControl.esign.clearActiveSignatureButton();
                    return;
                }

                signatureField.getName(function (name) {
                    TextControl.esign.highlightSignatureButton(name || null);
                });
            }, function () {
                TextControl.esign.clearActiveSignatureButton();
            });
        });

        TXTextControl.addEventListener("signatureFieldDeselected", function () {
            TextControl.esign.clearActiveSignatureButton();
        });
    }

    function initEnvelopeEditor(envelopeId) {
        TXTextControl.addEventListener("textControlLoaded", function () {
            TextControl.esign.getDocument(envelopeId, "envelope");
            initSignatureFieldSidebarTracking();
        });

        TXTextControl.addEventListener("ribbonTabsLoaded", function () {
            TXTextControl.addEventListener("textFieldEntered", function (ff) {
                TXTextControl.ribbon.selectedTab = "tabFormFields";

                if (ff.textField.type === "TEXTFORMFIELD" || ff.textField.type === "DATEFORMFIELD") {
                    $("#fieldProperties").removeClass("d-none");
                    $("#fieldRequired").prop("checked", ff.textField.id === 1);
                }
            });

            initRequiredFieldBehavior();

            showEditorChrome();
        });

        window.saveDocument = function () {
            TXTextControl.saveDocument(32, function (document) {
                TextControl.esign.saveEditor(document.data, envelopeId);
            });
        };
    }

    function initTemplateEditor(templateId) {
        var curField;

        TXTextControl.addEventListener("textControlLoaded", function () {
            TextControl.esign.getDocument(templateId, "template");
            enableTooltips();
        });

        TXTextControl.addEventListener("ribbonTabsLoaded", function () {
            TXTextControl.addEventListener("textFieldEntered", function (ff) {
                if (ff.textField.type !== "TEXTFORMFIELD" &&
                    ff.textField.type !== "DATEFORMFIELD" &&
                    ff.textField.type !== "APPLICATIONFIELD") {
                    return;
                }

                curField = ff.textField;
                $("#fieldProperties").removeClass("d-none");
                $("#fieldName").val(ff.textField.name);
                $("#fieldNameApply").off("click.esign").on("click.esign", function () {
                    var newValue = $("#fieldName").val();

                    if (curField.type === "APPLICATIONFIELD") {
                        TXTextControl.applicationFields.getItem(function (af) {
                            if (af === null) return;
                            af.getParameters(function (par) {
                                par[0] = newValue;
                                af.setParameters(par);
                                TextControl.esign.showToast("Field name applied.");
                            });
                        });
                    }

                    if (curField.type === "TEXTFORMFIELD" || curField.type === "DATEFORMFIELD") {
                        TXTextControl.formFields.getItem(function (af) {
                            af.setName(newValue);
                            TextControl.esign.showToast("Field name applied.");
                        });
                    }
                });

                if (ff.textField.type === "TEXTFORMFIELD" || ff.textField.type === "DATEFORMFIELD") {
                    $("#fieldRequired").removeAttr("disabled");
                    $("#fieldRequired").prop("checked", ff.textField.id === 1);
                }
                else {
                    $("#fieldRequired").attr("disabled", "disabled");
                }
            });

            initRequiredFieldBehavior();
            showEditorChrome();
        });

        window.saveDocument = function () {
            TXTextControl.saveDocument(32, function (document) {
                TextControl.esign.saveTemplate(document.data, templateId);
            });
        };
    }

    function initContractEditor(contractId) {
        var curField;

        TXTextControl.addEventListener("textControlLoaded", function () {
            TextControl.esign.getDocument(contractId, "contract");
            enableTooltips();
        });

        TXTextControl.addEventListener("ribbonTabsLoaded", function () {
            TXTextControl.addEventListener("textFieldEntered", function (ff) {
                if (ff.textField.type !== "TEXTFORMFIELD" &&
                    ff.textField.type !== "DATEFORMFIELD" &&
                    ff.textField.type !== "APPLICATIONFIELD") {
                    return;
                }

                curField = ff.textField;
                $("#fieldProperties").removeClass("d-none");
                $("#fieldName").val(ff.textField.name);
                $("#fieldNameApply").off("click.esign").on("click.esign", function () {
                    var newValue = $("#fieldName").val();

                    if (curField.type === "APPLICATIONFIELD") {
                        TXTextControl.applicationFields.getItem(function (af) {
                            if (af === null) return;
                            af.getParameters(function (par) {
                                par[0] = newValue;
                                af.setParameters(par);
                                TextControl.esign.showToast("Field name applied.");
                            });
                        });
                    }

                    if (curField.type === "TEXTFORMFIELD" || curField.type === "DATEFORMFIELD") {
                        TXTextControl.formFields.getItem(function (af) {
                            af.setName(newValue);
                            TextControl.esign.showToast("Field name applied.");
                        });
                    }
                });

                if (ff.textField.type === "TEXTFORMFIELD" || ff.textField.type === "DATEFORMFIELD") {
                    $("#fieldRequired").removeAttr("disabled");
                    $("#fieldRequired").prop("checked", ff.textField.id === 1);
                }
                else {
                    $("#fieldRequired").attr("disabled", "disabled");
                }
            });

            initRequiredFieldBehavior();
            showEditorChrome();
        });

        window.saveDocument = function () {
            TXTextControl.saveDocument(32, function (document) {
                TextControl.esign.saveContractEditor(document.data, contractId);
            });
        };
    }

    window.TextControlEsignPages = {
        initReviewSigning: function () {
            window.addEventListener("documentViewerLoaded", function () {
                var signingInProgress = false;
                TXDocumentViewer.toolbar.hide(); 
                function showSigningState(state, message) {
                    var result = document.getElementById("reviewSigningResult");
                    var viewer = document.getElementById("home");
                    var icon = document.getElementById("reviewSigningResultIcon");
                    var title = document.getElementById("reviewSigningResultTitle");
                    var text = document.getElementById("reviewSigningResultMessage");
                    var detail = document.getElementById("reviewSigningResultDetail");
                    var accountPrompt = document.getElementById("reviewSignerAccountPrompt");
                    var email = result ? result.getAttribute("data-signer-email") : "";
                    var isSuccess = state === "success";
                    var isError = state === "error";

                    if (!result || !icon || !title || !text || !detail) {
                        TextControl.esign.showToast(message, isError ? "danger" : undefined);
                        return;
                    }

                    if (viewer && state !== "pending") {
                        viewer.classList.add("d-none");
                    }

                    result.classList.remove("d-none", "review-sign-result-success", "review-sign-result-error", "review-sign-result-progress");
                    result.classList.add("review-sign-result-" + state);

                    if (state === "pending") {
                        icon.innerHTML = '<div class="spinner-border" role="status"><span class="visually-hidden">Signing...</span></div>';
                        title.textContent = "Signing Document";
                        text.textContent = "Please wait while your signature is applied and the document is finalized.";
                    }
                    else if (isSuccess) {
                        icon.innerHTML = '<i class="bi bi-check2-circle"></i>';
                        title.textContent = "Document Signed";
                        text.textContent = "Thank you. Your signature has been recorded" + (email ? " and a confirmation has been sent to " + email + "." : ".");
                    }
                    else {
                        icon.innerHTML = '<i class="bi bi-exclamation-triangle"></i>';
                        title.textContent = "Signing Could Not Be Completed";
                        text.textContent = "The document could not be finalized. Don't worry, the sender has been notified.";
                    }

                    if (!isError) {
                        detail.classList.add("d-none");
                        detail.textContent = "";
                    }
                    else {
                        detail.classList.remove("d-none");
                        detail.textContent = message || "Please close this page. The sender can review the issue in the envelope overview.";
                    }

                    if (accountPrompt) {
                        accountPrompt.classList.toggle("d-none", !isSuccess);
                    }
                }

                function isSubmitAction(element) {
                    var action = element && element.closest("button, input[type='button'], input[type='submit'], a, [role='button']");
                    if (!action) {
                        return false;
                    }

                    var label = (action.innerText || action.value || action.getAttribute("aria-label") || action.getAttribute("title") || "").trim().toLowerCase();
                    return label === "submit" || label.indexOf("submit") >= 0 || label.indexOf("sign") >= 0;
                }

                document.addEventListener("click", function (event) {
                    if (signingInProgress || !event.target.closest("#tx-documentViewer")) {
                        return;
                    }

                    if (isSubmitAction(event.target)) {
                        signingInProgress = true;
                        showSigningState("pending");
                    }
                }, true);

                TXDocumentViewer.signatures.setSubmitCallback(function (result) {
                    if (result === true || result == "true") {
                        showSigningState("success");
                    }
                    else {
                        signingInProgress = false;
                        showSigningState("error", result || "The document could not be signed.");
                    }
                });

                $("#tx-documentViewer").css("z-index", 800);

                TXDocumentViewer.signatures.setBeforeSubmitCallback(function () {
                    if (signingInProgress) {
                        TextControl.esign.showToast("The document is already being submitted.", "warning");
                        return false;
                    }

                    signingInProgress = true;
                    showSigningState("pending");
                    return true;
                });
            });
        },

        initCollaboration: function (documentId, owner) {
            window.acceptAll = function () {
                removeAllChanges(true);
            };

            window.makeChanges = function () {
                TXTextControl.editMode = 1;
                $("#btnAccept").html("<strong>Save and Propose Changes</strong>");
                $("#btnChanges").hide();
                TextControl.esign.showToast("Document unlocked. You can make changes to the document now!");
            };

            window.saveDocument = function () {
                TXTextControl.saveDocument(32, function (document) {
                    TextControl.esign.saveContract(document.data, documentId, owner);
                });
            };

            function removeAllChanges(accept) {
                TXTextControl.trackedChanges.getCount(function (count) {
                    if (count === 0) return;

                    TXTextControl.trackedChanges.elementAt(0, function (element) {
                        TXTextControl.trackedChanges.remove(element, accept, function (deleted) {
                            if (deleted === true) removeAllChanges(accept);
                        });
                    });
                });
            }

            TXTextControl.addEventListener("textControlLoaded", function () {
                TextControl.esign.getContract(documentId);
            });

            TXTextControl.addEventListener("ribbonTabsLoaded", function () {
                $("#ribbonTabProofing_btnTrackChanges").hide();

                TXTextControl.editMode = 3;
                TXTextControl.isTrackChangesEnabled = true;
                TXTextControl.showSideBar(TXTextControl.SideBarType.TrackChanges, 1);

                TextControl.esign.showToast("Document is locked. To propose changes, click the 'Make Changes' button!");
                showEditorChrome();

                TXTextControl.trackedChanges.getCount(function (count) {
                    if (count > 0) {
                        $("#btnAcceptAll").removeAttr("disabled");
                    }
                });
            });
        }
    };
}());
