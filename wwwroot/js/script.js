document.addEventListener('DOMContentLoaded', () => {
    const loginForm = document.getElementById('loginForm');
    const loginMessage = document.getElementById('loginMessage');
    const logoutButton = document.getElementById('logoutButton');
    const el = document.getElementById("wrapper");
    const toggleButton = document.getElementById("menu-toggle");

    console.log('DOM Content Loaded.');
    console.log('Wrapper element:', el);
    console.log('Toggle button element:', toggleButton);
    console.log('Navbar status element:', document.getElementById('navbarStatus'));

    if (loginForm) {
        loginForm.addEventListener('submit', async (e) => {
            e.preventDefault();

            const email = document.getElementById('email').value;
            const password = document.getElementById('password').value;

            loginMessage.classList.add('d-none'); // Hide previous messages

            try {
                const response = await fetch('http://localhost:5110/api/v1/auth/admin/login', {
                    method: 'POST',
                    headers: {
                        'Content-Type': 'application/json',
                    },
                    body: JSON.stringify({ email, password }),
                });

                const data = await response.json();

                console.log('API Login Response Data:', data);
                console.log('Token from API:', data.data.token);

                if (response.ok) {
                    // Send token and expires_in to PHP to store in session
                    const setSessionResponse = await fetch('set_session.php', {
                        method: 'POST',
                        headers: {
                            'Content-Type': 'application/json',
                        },
                        body: JSON.stringify({ token: data.data.token, expires_in: data.data.expires_in }),
                    });

                    const setSessionData = await setSessionResponse.json();
                    console.log('set_session.php Response Data:', setSessionData);

                    if (setSessionResponse.ok && setSessionData.status === 'success') {
                        window.location.href = 'dashboard.php'; // Redirect to dashboard
                    } else {
                        console.error('Erro ao salvar token na sessão PHP:', setSessionData.message);
                        loginMessage.textContent = 'Erro interno. Tente novamente.';
                        loginMessage.classList.remove('d-none');
                    }
                } else {
                    loginMessage.textContent = data.message || 'Erro ao fazer login. Verifique suas credenciais.';
                    loginMessage.classList.remove('d-none');
                }
            } catch (error) {
                console.error('Erro na requisição:', error);
                loginMessage.textContent = 'Não foi possível conectar ao servidor. Tente novamente mais tarde.';
                loginMessage.classList.remove('d-none');
            }
        });
    }

    if (logoutButton) {
        logoutButton.addEventListener('click', async (e) => {
            e.preventDefault();
            sessionStorage.removeItem('admin_token'); // Clear token from session storage
            window.location.href = '../../logout.php';
        });
    }

    // Toggle sidebar - only if elements exist
    if (toggleButton && el) {
        toggleButton.addEventListener('click', function () {
            console.log('Toggle button clicked!');
            el.classList.toggle("toggled");
            console.log('Wrapper class list after toggle:', el.classList);
        });
    }

    // Timer and API Status Update - only if elements exist and variables are passed
    const timerElement = document.getElementById('tokenTimer');
    const apiStatusElement = document.getElementById('apiStatus');
    const apiResponseTimeElement = document.getElementById('apiResponseTime');
    const apiLastCheckElement = document.getElementById('apiLastCheck');
    const sidebarApiStatusElement = document.getElementById('sidebarApiStatus');
    const sidebarSystemStatusElement = document.getElementById('sidebarSystemStatus');
    const sidebarLastUpdateElement = document.getElementById('sidebarLastUpdate');
    const updateStatusButton = document.getElementById('updateStatusButton');
    const navbarStatus = document.getElementById('navbarStatus');

    // Check if phpTokenExpiresIn and phpTokenStartTime are defined globally (from dashboard.php)
    if (typeof phpTokenExpiresIn !== 'undefined' && typeof phpTokenStartTime !== 'undefined') {
        let tokenExpiresIn = phpTokenExpiresIn;
        let tokenStartTime = phpTokenStartTime;

        function updateTimer() {
            if (tokenExpiresIn !== null && tokenStartTime !== null) {
                const currentTime = Math.floor(Date.now() / 1000);
                const elapsedTime = currentTime - tokenStartTime;
                const remainingTime = tokenExpiresIn - elapsedTime;

                if (remainingTime <= 0) {
                    if (timerElement) timerElement.textContent = '00:00:00';
                    // Optionally, redirect to login or refresh token
                    // window.location.href = 'logout.php';
                } else {
                    const hours = Math.floor(remainingTime / 3600);
                    const minutes = Math.floor((remainingTime % 3600) / 60);
                    const seconds = remainingTime % 60;

                    const formattedTime = [
                        hours.toString().padStart(2, '0'),
                        minutes.toString().padStart(2, '0'),
                        seconds.toString().padStart(2, '0')
                    ].join(':');

                    if (timerElement) timerElement.textContent = formattedTime;
                }
            }
        }

        // Initial call and update every second
        updateTimer();
        setInterval(updateTimer, 1000);

        // Function to fetch and update API status
        async function fetchAndUpdateApiStatus() {
            // Only proceed if we have at least one element to update
            if (!timerElement && !apiStatusElement && !navbarStatus && !sidebarApiStatusElement) {
                console.log('No status elements found, skipping API status update');
                return;
            }
            const updateStatus = (isOnline, data = {}) => {
                const status = isOnline ? 'Online' : 'Offline';
                const badgeClass = isOnline ? 'bg-success' : 'bg-danger';
                const responseTime = isOnline ? `${data.response_time || 'N/A'}ms` : 'N/A';
                const timestamp = data.timestamp || new Date().toISOString();
                const displayTime = new Date(timestamp).toLocaleTimeString('pt-BR');

                if (apiStatusElement) {
                    apiStatusElement.textContent = status;
                    apiStatusElement.className = `badge ${badgeClass}`;
                }
                if (apiResponseTimeElement) apiResponseTimeElement.textContent = responseTime;
                if (apiLastCheckElement) apiLastCheckElement.textContent = timestamp;

                if (sidebarApiStatusElement) sidebarApiStatusElement.textContent = `API: ${status}`;
                if (sidebarSystemStatusElement) sidebarSystemStatusElement.textContent = `Sistema: ${status}`;
                if (sidebarLastUpdateElement) sidebarLastUpdateElement.textContent = `Última atualização: ${displayTime}`;

                if (navbarStatus) {
                    navbarStatus.innerHTML = `<i class="bi bi-circle-fill me-1"></i>${status}`;
                    navbarStatus.className = `navbar-text ${isOnline ? 'text-success' : 'text-danger'}`;
                } else {
                    console.log('navbarStatus element not found in DOM');
                }
            };

            try {
                const response = await fetch('http://localhost:5110/api/v1/status');
                const data = await response.json();

                if (response.ok && data.success === true) {
                    updateStatus(true, data);
                } else {
                    updateStatus(false);
                }
            } catch (error) {
                console.error('Error fetching API status:', error);
                updateStatus(false);
            }
        }

        // Initial fetch of API status
        fetchAndUpdateApiStatus();

        // Update API status every 30 seconds (adjust as needed)
        setInterval(fetchAndUpdateApiStatus, 30000);

        // Event listener for update status button
        if (updateStatusButton) {
            updateStatusButton.addEventListener('click', fetchAndUpdateApiStatus);
        }
    }

    // Generic API call function for client-side operations
    async function callApi(method, endpoint, data = null) {
        const token = typeof phpAdminToken !== 'undefined' ? phpAdminToken : null; // Get token from global PHP variable
        const headers = {
            'Content-Type': 'application/json',
        };
        if (token) {
            headers['Authorization'] = `Bearer ${token}`;
        }

        const options = {
            method: method,
            headers: headers,
            body: data ? JSON.stringify(data) : null,
        };

        try {
            const response = await fetch(`http://localhost:5110/api/v1/admin/${endpoint}`, options);
            const responseData = await response.json();
            if (!response.ok || responseData.success === false) {
                throw new Error(responseData.errors.message || responseData.message || 'API request failed');
            }
            return responseData;
        } catch (error) {
            console.error(`Error calling API ${endpoint}:`, error);
            alert(`Erro: ${error.message}`);
            throw error;
        }
    }

    // Cargos Page Logic - only if elements exist
    const cargosTableBody = document.getElementById('cargosTableBody');
    if (cargosTableBody) {
        const cargoModal = new bootstrap.Modal(document.getElementById('cargoModal'));
        const cargoForm = document.getElementById('cargoForm');
        const cargoIdInput = document.getElementById('cargoId');
        const electionIdInput = document.getElementById('electionId');
        const titleInput = document.getElementById('title');
        const descriptionInput = document.getElementById('description');
        const orderPositionInput = document.getElementById('orderPosition');
        const maxCandidatesInput = document.getElementById('maxCandidates');
        const minVotesInput = document.getElementById('minVotes');
        const maxVotesInput = document.getElementById('maxVotes');
        const addCargoBtn = document.getElementById('addCargoBtn');

        // Function to render cargos table
        async function renderCargos() {
            try {
                const response = await callApi('GET', 'positions');
                const cargos = response.data;
                cargosTableBody.innerHTML = '';
                if (cargos && cargos.length > 0) {
                    cargos.forEach(cargo => {
                        const row = `
                            <tr data-id="${cargo.id}">
                                <td>${cargo.id}</td>
                                <td>${cargo.title}</td>
                                <td>${cargo.description || ''}</td>
                                <td>${cargo.election_id}</td>
                                <td>${cargo.order_position}</td>
                                <td>${cargo.max_candidates}</td>
                                <td>${cargo.min_votes}</td>
                                <td>${cargo.max_votes}</td>
                                <td>
                                    <button class="btn btn-sm btn-info edit-cargo-btn" data-id="${cargo.id}"><i class="bi bi-pencil"></i></button>
                                    <button class="btn btn-sm btn-danger delete-cargo-btn" data-id="${cargo.id}"><i class="bi bi-trash"></i></button>
                                </td>
                            </tr>
                        `;
                        cargosTableBody.insertAdjacentHTML('beforeend', row);
                    });
                } else {
                    cargosTableBody.innerHTML = '<tr><td colspan="9" class="text-center">Nenhum cargo encontrado.</td></tr>';
                }
            } catch (error) {
                console.error('Error rendering cargos:', error);
                cargosTableBody.innerHTML = '<tr><td colspan="9" class="text-center text-danger">Erro ao carregar cargos.</td></tr>';
            }
        }

        // Add/Edit Cargo Form Submission
        cargoForm.addEventListener('submit', async (e) => {
            e.preventDefault();

            const id = cargoIdInput.value;
            const method = id ? 'PUT' : 'POST';
            const endpoint = id ? `positions/${id}` : 'positions';
            const cargoData = {
                election_id: parseInt(electionIdInput.value),
                title: titleInput.value,
                description: descriptionInput.value,
                order_position: parseInt(orderPositionInput.value),
                max_candidates: parseInt(maxCandidatesInput.value),
                min_votes: parseInt(minVotesInput.value),
                max_votes: parseInt(maxVotesInput.value),
            };

            try {
                await callApi(method, endpoint, cargoData);
                cargoModal.hide();
                renderCargos();
                cargoForm.reset();
            } catch (error) {
                console.error('Error saving cargo:', error);
            }
        });

        // Edit Cargo Button Click
        cargosTableBody.addEventListener('click', async (e) => {
            if (e.target.classList.contains('edit-cargo-btn') || e.target.closest('.edit-cargo-btn')) {
                const id = e.target.dataset.id || e.target.closest('.edit-cargo-btn').dataset.id;
                try {
                    const response = await callApi('GET', `positions/${id}`);
                    const cargo = response.data;
                    cargoIdInput.value = cargo.id;
                    electionIdInput.value = cargo.election_id;
                    titleInput.value = cargo.title;
                    descriptionInput.value = cargo.description;
                    orderPositionInput.value = cargo.order_position;
                    maxCandidatesInput.value = cargo.max_candidates;
                    minVotesInput.value = cargo.min_votes;
                    maxVotesInput.value = cargo.max_votes;
                    cargoModal.show();
                } catch (error) {
                    console.error('Error fetching cargo for edit:', error);
                }
            }
        });

        // Delete Cargo Button Click
        cargosTableBody.addEventListener('click', async (e) => {
            if (e.target.classList.contains('delete-cargo-btn') || e.target.closest('.delete-cargo-btn')) {
                const id = e.target.dataset.id || e.target.closest('.delete-cargo-btn').dataset.id;
                if (confirm('Tem certeza que deseja excluir este cargo?')) {
                    try {
                        await callApi('DELETE', `positions/${id}`);
                        renderCargos();
                    } catch (error) {
                        console.error('Error deleting cargo:', error);
                    }
                }
            }
        });

        // Reset form when modal is hidden
        document.getElementById('cargoModal').addEventListener('hidden.bs.modal', function () {
            cargoForm.reset();
            cargoIdInput.value = '';
        });

        // Initial render for cargos page
        renderCargos();
    }

    // Votantes Page Logic - only if elements exist
    const votantesTableBody = document.getElementById('votantesTableBody');
    if (votantesTableBody) {
        // Function to render votantes table
        async function renderVotantes() {
            try {
                const response = await callApi('GET', 'voters');
                const votantes = response.data.data; // Access the 'data' array within the response
                votantesTableBody.innerHTML = '';
                if (votantes && votantes.length > 0) {
                    votantes.forEach(votante => {
                        const row = `
                            <tr data-id="${votante.id}">
                                <td>${votante.id}</td>
                                <td><input type="text" class="form-control" value="${votante.name || ''}" data-field="name"></td>
                                <td><input type="text" class="form-control" value="${votante.matriculation || 'N/A'}" data-field="matriculation"></td>
                                <td><input type="text" class="form-control" value="${votante.cpf || ''}" data-field="cpf"></td>
                                <td><input type="date" class="form-control" value="${votante.birth_date || ''}" data-field="birth_date"></td>
                                <td><input type="email" class="form-control" value="${votante.email || ''}" data-field="email"></td>
                                <td><input type="text" class="form-control" value="${votante.phone || ''}" data-field="phone"></td>
                                <td><input type="number" step="0.1" class="form-control" value="${votante.vote_weight || ''}" data-field="vote_weight"></td>
                                <td>
                                    <button class="btn btn-sm btn-primary save-votante-btn" data-id="${votante.id}"><i class="bi bi-save"></i> Save</button>
                                    <button class="btn btn-sm btn-danger delete-votante-btn" data-id="${votante.id}"><i class="bi bi-trash"></i> Delete</button>
                                </td>
                            </tr>
                        `;
                        votantesTableBody.insertAdjacentHTML('beforeend', row);
                    });
                } else {
                    votantesTableBody.innerHTML = '<tr><td colspan="9" class="text-center">Nenhum votante encontrado.</td></tr>';
                }
            } catch (error) {
                console.error('Error rendering votantes:', error);
                votantesTableBody.innerHTML = '<tr><td colspan="9" class="text-center text-danger">Erro ao carregar votantes.</td></tr>';
            }
        }

        // Save Votante Button Click
        votantesTableBody.addEventListener('click', async (e) => {
            if (e.target.classList.contains('save-votante-btn') || e.target.closest('.save-votante-btn')) {
                const id = e.target.dataset.id || e.target.closest('.save-votante-btn').dataset.id;
                const row = e.target.closest('tr');
                const votanteData = {};
                row.querySelectorAll('input[data-field]').forEach(input => {
                    votanteData[input.dataset.field] = input.value;
                });

                try {
                    await callApi('PUT', `voters/${id}`, votanteData);
                    alert('Votante salvo com sucesso!');
                    renderVotantes(); // Re-render to show updated data
                } catch (error) {
                    console.error('Error saving votante:', error);
                    alert(`Erro ao salvar votante: ${error.message}`);
                }
            }
        });

        // Delete Votante Button Click
        votantesTableBody.addEventListener('click', async (e) => {
            if (e.target.classList.contains('delete-votante-btn') || e.target.closest('.delete-votante-btn')) {
                const id = e.target.dataset.id || e.target.closest('.delete-votante-btn').dataset.id;
                if (confirm('Tem certeza que deseja excluir este votante?')) {
                    try {
                        await callApi('DELETE', `voters/${id}`);
                        alert('Votante excluído com sucesso!');
                        renderVotantes(); // Re-render to show updated data
                    } catch (error) {
                        console.error('Error deleting votante:', error);
                        alert(`Erro ao excluir votante: ${error.message}`);
                    }
                }
            }
        });

        // Initial render for votantes page
        renderVotantes();
    }

    // Candidatos Page Logic - only if elements exist
    const candidatesTableBody = document.getElementById('candidatesTableBody');
    if (candidatesTableBody) {
        const candidateForm = document.getElementById('candidateForm');
        const candidateIdInput = document.getElementById('candidateId');
        const fullNameInput = document.getElementById('fullName');
        const positionSelect = document.getElementById('position');
        const candidateNumberInput = document.getElementById('candidateNumber');
        const candidatePhotoInput = document.getElementById('candidatePhoto');
        const biographyInput = document.getElementById('biography');
        const addCandidateBtn = document.getElementById('addCandidateBtn');

        // Function to load positions into the dropdown
        async function loadPositions() {
            try {
                const response = await callApi('GET', 'positions');
                const positions = response.data;
                positionSelect.innerHTML = '<option value="">Selecione o Cargo</option>'; // Clear existing options
                if (positions && positions.length > 0) {
                    positions.forEach(position => {
                        const option = document.createElement('option');
                        option.value = position.id;
                        option.textContent = position.title;
                        positionSelect.appendChild(option);
                    });
                }
            } catch (error) {
                console.error('Error loading positions:', error);
            }
        }

        // Function to render candidates table
        async function renderCandidates() {
            candidatesTableBody.innerHTML = '<tr><td colspan="5" class="text-center">Carregando candidatos...</td></tr>';
            try {
                const response = await callApi('GET', 'candidates');
                const candidates = response.data;
                candidatesTableBody.innerHTML = ''; // Clear loading message

                if (candidates && candidates.length > 0) {
                    for (const candidate of candidates) {
                        const photoUrl = candidate.photo_url ? `http://localhost:5110${candidate.photo_url}` : 'https://via.placeholder.com/50?text=No+Photo'; // Placeholder for no photo
                        const positionName = await getPositionName(candidate.position_id);

                        const row = `
                            <tr data-id="${candidate.id}">
                                <td><img src="${photoUrl}" alt="Foto do Candidato" width="50" height="50" class="rounded-circle"></td>
                                <td>${candidate.name}</td>
                                <td>${candidate.number || ''}</td>
                                <td>${positionName}</td>
                                <td>
                                    <button class="btn btn-sm btn-info edit-candidate-btn" data-id="${candidate.id}"><i class="bi bi-pencil"></i></button>
                                    <button class="btn btn-sm btn-danger delete-candidate-btn" data-id="${candidate.id}"><i class="bi bi-trash"></i></button>
                                </td>
                            </tr>
                        `;
                        candidatesTableBody.insertAdjacentHTML('beforeend', row);
                    }
                } else {
                    candidatesTableBody.innerHTML = '<tr><td colspan="5" class="text-center">Nenhum candidato encontrado.</td></tr>';
                }
            } catch (error) {
                console.error('Error rendering candidates:', error);
                candidatesTableBody.innerHTML = '<tr><td colspan="5" class="text-center text-danger">Erro ao carregar candidatos.</td></tr>';
            }
        }

        // Helper function to get position name (to avoid multiple API calls for same position)
        const positionCache = {};
        async function getPositionName(positionId) {
            if (positionCache[positionId]) {
                return positionCache[positionId];
            }
            try {
                const response = await callApi('GET', `positions/${positionId}`);
                const position = response.data;
                positionCache[positionId] = position.title;
                return position.title;
            } catch (error) {
                console.error(`Error fetching position ${positionId}:`, error);
                return 'N/A';
            }
        }

        // Add/Edit Candidate Form Submission
        candidateForm.addEventListener('submit', async (e) => {
            e.preventDefault();

            const id = candidateIdInput.value;
            const method = id ? 'PUT' : 'POST';
            const endpoint = id ? `candidates/${id}` : 'candidates';
            const candidateData = {
                position_id: parseInt(positionSelect.value),
                name: fullNameInput.value,
                number: candidateNumberInput.value,
                description: biographyInput.value,
            };

            try {
                const response = await callApi(method, endpoint, candidateData);
                const candidateId = response.data.id || id; // Get ID for photo upload

                // Handle photo upload if a file is selected
                if (candidatePhotoInput.files.length > 0) {
                    const photoFile = candidatePhotoInput.files[0];
                    const formData = new FormData();
                    formData.append('photo', photoFile);

                    // Manual fetch for multipart/form-data
                    const photoUploadResponse = await fetch(`http://localhost:5110/api/v1/admin/candidates/${candidateId}/photo`, {
                        method: 'POST',
                        headers: {
                            'Authorization': `Bearer ${typeof phpAdminToken !== 'undefined' ? phpAdminToken : null}`,
                            // 'Content-Type': 'multipart/form-data' - DO NOT SET THIS HEADER, BROWSER DOES IT AUTOMATICALLY
                        },
                        body: formData,
                    });

                    if (!photoUploadResponse.ok) {
                        const errorData = await photoUploadResponse.json();
                        console.error('Error uploading photo:', errorData);
                        alert(`Erro ao fazer upload da foto: ${errorData.message || 'Erro desconhecido'}`);
                    }
                }

                candidateForm.reset();
                candidateIdInput.value = ''; // Clear hidden ID
                await renderCandidates();
                await loadPositions(); // Reload positions in case of new elections/positions
            } catch (error) {
                console.error('Error saving candidate:', error);
            }
        });

        // Edit Candidate Button Click
        candidatesTableBody.addEventListener('click', async (e) => {
            if (e.target.classList.contains('edit-candidate-btn') || e.target.closest('.edit-candidate-btn')) {
                const id = e.target.dataset.id || e.target.closest('.edit-candidate-btn').dataset.id;
                try {
                    const response = await callApi('GET', `candidates/${id}`);
                    const candidate = response.data;
                    candidateIdInput.value = candidate.id;
                    fullNameInput.value = candidate.name;
                    positionSelect.value = candidate.position_id;
                    candidateNumberInput.value = candidate.number;
                    biographyInput.value = candidate.description;
                    // Photo input cannot be pre-filled for security reasons
                } catch (error) {
                    console.error('Error fetching candidate for edit:', error);
                }
            }
        });

        // Delete Candidate Button Click
        candidatesTableBody.addEventListener('click', async (e) => {
            if (e.target.classList.contains('delete-candidate-btn') || e.target.closest('.delete-candidate-btn')) {
                const id = e.target.dataset.id || e.target.closest('.delete-candidate-btn').dataset.id;
                if (confirm('Tem certeza que deseja excluir este candidato?')) {
                    try {
                        await callApi('DELETE', `candidates/${id}`);
                        await renderCandidates();
                    } catch (error) {
                        console.error('Error deleting candidate:', error);
                    }
                }
            }
        });

        // Initial load
        loadPositions();
        renderCandidates();
    }
});