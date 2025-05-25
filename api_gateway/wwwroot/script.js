// API Base URL
const API_BASE = '';

// Global state
let uploadedFiles = [];

// DOM Elements
const fileInput = document.getElementById('fileInput');
const uploadArea = document.getElementById('uploadArea');
const uploadProgress = document.getElementById('uploadProgress');
const progressFill = document.getElementById('progressFill');
const progressText = document.getElementById('progressText');
const filesGrid = document.getElementById('filesGrid');
const file1Select = document.getElementById('file1Select');
const file2Select = document.getElementById('file2Select');
const comparisonResults = document.getElementById('comparisonResults');
const loadingOverlay = document.getElementById('loadingOverlay');

// Initialize
document.addEventListener('DOMContentLoaded', function() {
    initializeEventListeners();
    loadUploadedFiles();
});

// Event Listeners
function initializeEventListeners() {
    // File input change
    fileInput.addEventListener('change', handleFileSelect);
    
    // Drag and drop
    uploadArea.addEventListener('dragover', handleDragOver);
    uploadArea.addEventListener('dragleave', handleDragLeave);
    uploadArea.addEventListener('drop', handleFileDrop);
    
    // Upload area click
    uploadArea.addEventListener('click', () => fileInput.click());
    
    // Smooth scrolling for navigation
    document.querySelectorAll('.nav-link').forEach(link => {
        link.addEventListener('click', function(e) {
            e.preventDefault();
            const target = document.querySelector(this.getAttribute('href'));
            if (target) {
                target.scrollIntoView({ behavior: 'smooth' });
            }
        });
    });
}

// Drag and Drop Handlers
function handleDragOver(e) {
    e.preventDefault();
    uploadArea.classList.add('dragover');
}

function handleDragLeave(e) {
    e.preventDefault();
    uploadArea.classList.remove('dragover');
}

function handleFileDrop(e) {
    e.preventDefault();
    uploadArea.classList.remove('dragover');
    
    const files = e.dataTransfer.files;
    if (files.length > 0) {
        handleFileUpload(files[0]);
    }
}

// File Selection Handler
function handleFileSelect(e) {
    const file = e.target.files[0];
    if (file) {
        handleFileUpload(file);
    }
}

// File Upload
async function handleFileUpload(file) {
    // Validation
    if (!file.name.endsWith('.txt')) {
        showToast('Поддерживаются только файлы .txt', 'error');
        return;
    }
    
    if (file.size > 10 * 1024 * 1024) { // 10MB
        showToast('Размер файла не должен превышать 10 МБ', 'error');
        return;
    }
    
    showLoadingOverlay(true);
    showUploadProgress(true);
    
    try {
        const formData = new FormData();
        formData.append('file', file);
        
        const response = await fetch(`${API_BASE}/upload`, {
            method: 'POST',
            body: formData
        });
        
        const result = await response.json();
        
        if (response.ok) {
            if (result.duplicate_of) {
                showToast(`Файл является дубликатом ${result.duplicate_of}`, 'warning');
                addDuplicateFile(file.name, result.duplicate_of);
            } else {
                showToast('Файл успешно загружен и проанализирован!', 'success');
                addUploadedFile(file.name, result.file_id, result.stats);
                updateFileSelectors();
            }
        } else {
            throw new Error(result.error || 'Ошибка загрузки файла');
        }
    } catch (error) {
        console.error('Upload error:', error);
        showToast(`Ошибка: ${error.message}`, 'error');
    } finally {
        showLoadingOverlay(false);
        showUploadProgress(false);
        fileInput.value = '';
    }
}

// Show/Hide Upload Progress
function showUploadProgress(show) {
    uploadProgress.style.display = show ? 'block' : 'none';
    if (show) {
        let progress = 0;
        const interval = setInterval(() => {
            progress += Math.random() * 20;
            if (progress >= 90) {
                clearInterval(interval);
                progressFill.style.width = '90%';
                progressText.textContent = 'Анализ файла...';
            } else {
                progressFill.style.width = `${progress}%`;
                progressText.textContent = `Загрузка... ${Math.round(progress)}%`;
            }
        }, 200);
    } else {
        progressFill.style.width = '100%';
        progressText.textContent = 'Завершено!';
        setTimeout(() => {
            progressFill.style.width = '0%';
            progressText.textContent = 'Загрузка...';
        }, 1000);
    }
}

// Add Uploaded File to List
function addUploadedFile(fileName, fileId, stats) {
    const fileData = {
        id: fileId,
        name: fileName,
        stats: stats,
        uploadDate: new Date(),
        isDuplicate: false
    };
    
    uploadedFiles.push(fileData);
    saveToLocalStorage();
    renderFile(fileData);
}

// Add Duplicate File
function addDuplicateFile(fileName, originalFileId) {
    const fileData = {
        id: `duplicate-${Date.now()}`,
        name: fileName,
        originalFileId: originalFileId,
        uploadDate: new Date(),
        isDuplicate: true
    };
    
    uploadedFiles.push(fileData);
    saveToLocalStorage();
    renderFile(fileData);
}

// Render File Card
function renderFile(fileData) {
    const fileCard = document.createElement('div');
    fileCard.className = 'file-card fade-in';
    
    if (fileData.isDuplicate) {
        fileCard.innerHTML = `
            <div class="file-header">
                <div class="file-name">${fileData.name}</div>
                <div class="file-badge duplicate">Дубликат</div>
            </div>
            <p class="text-secondary">
                Дубликат файла: ${fileData.originalFileId}
            </p>
            <div class="file-actions">
                <button class="btn btn-outline btn-small" onclick="removeFile('${fileData.id}')">
                    <i class="fas fa-trash"></i> Удалить
                </button>
            </div>
        `;
    } else {
        fileCard.innerHTML = `
            <div class="file-header">
                <div class="file-name">${fileData.name}</div>
                <div class="file-badge">Проанализирован</div>
            </div>
            <div class="file-stats">
                <div class="stat-item">
                    <div class="stat-value">${fileData.stats.paragraphs}</div>
                    <div class="stat-label">Абзацы</div>
                </div>
                <div class="stat-item">
                    <div class="stat-value">${fileData.stats.words}</div>
                    <div class="stat-label">Слова</div>
                </div>
                <div class="stat-item">
                    <div class="stat-value">${fileData.stats.chars}</div>
                    <div class="stat-label">Символы</div>
                </div>
            </div>
            <div class="file-actions">
                <button class="btn btn-primary btn-small" onclick="generateWordCloud('${fileData.id}')">
                    <i class="fas fa-cloud"></i> Облако слов
                </button>
                <button class="btn btn-outline btn-small" onclick="removeFile('${fileData.id}')">
                    <i class="fas fa-trash"></i> Удалить
                </button>
            </div>
            <div id="wordcloud-${fileData.id}" class="word-cloud-container" style="display: none;"></div>
        `;
    }
    
    filesGrid.appendChild(fileCard);
}

// Generate Word Cloud
async function generateWordCloud(fileId) {
    const container = document.getElementById(`wordcloud-${fileId}`);
    
    try {
        const response = await fetch(`${API_BASE}/cloud/${fileId}`);
        const result = await response.json();
        
        if (response.ok) {
            container.innerHTML = `
                <h4>Облако слов</h4>
                <img src="${result.word_cloud_url}" alt="Word Cloud" class="word-cloud-img" />
            `;
            container.style.display = 'block';
            showToast('Облако слов сгенерировано!', 'success');
        } else {
            throw new Error(result.error || 'Ошибка генерации облака слов');
        }
    } catch (error) {
        console.error('Word cloud error:', error);
        showToast(`Ошибка: ${error.message}`, 'error');
    }
}

// Remove File
function removeFile(fileId) {
    console.log('removeFile called with fileId:', fileId);
    
    // Находим файл в массиве
    const file = uploadedFiles.find(f => f.id === fileId);
    console.log('Found file:', file);
    
    if (!file) {
        showToast('Файл не найден', 'error');
        return;
    }
    
    // Проверяем дубликат двумя способами: по флагу и по ID
    const isDuplicate = file.isDuplicate || fileId.startsWith('duplicate-');
    console.log('Is duplicate?', isDuplicate, 'isDuplicate flag:', file.isDuplicate, 'ID starts with duplicate-:', fileId.startsWith('duplicate-'));
    
    // Если это дубликат, удаляем только локально
    if (isDuplicate) {
        console.log('Removing duplicate locally only');
        uploadedFiles = uploadedFiles.filter(f => f.id !== fileId);
        saveToLocalStorage();
        updateFileSelectors();
        renderAllFiles();
        showToast('Дубликат удален', 'info');
        return;
    }
    
    // Если это настоящий файл, удаляем через API
    console.log('Removing real file through API');
    fetch(`${API_BASE}/files/${fileId}`, {
        method: 'DELETE'
    })
    .then(response => {
        if (response.ok) {
            // Удаляем из локального состояния только после успешного удаления с сервера
            uploadedFiles = uploadedFiles.filter(f => f.id !== fileId);
            saveToLocalStorage();
            updateFileSelectors();
            renderAllFiles();
            showToast('Файл удален', 'info');
        } else if (response.status === 404) {
            // Файл не найден на сервере - удаляем из localStorage
            console.log('File not found on server, removing from localStorage');
            uploadedFiles = uploadedFiles.filter(f => f.id !== fileId);
            saveToLocalStorage();
            updateFileSelectors();
            renderAllFiles();
            showToast('Файл не найден на сервере и удален из списка', 'warning');
        } else {
            showToast('Ошибка при удалении файла', 'error');
        }
    })
    .catch(error => {
        console.error('Error deleting file:', error);
        showToast('Ошибка при удалении файла', 'error');
    });
}

// Update File Selectors for Comparison
function updateFileSelectors() {
    const nonDuplicateFiles = uploadedFiles.filter(file => !file.isDuplicate);
    
    // Clear existing options
    file1Select.innerHTML = '<option value="">Выберите файл</option>';
    file2Select.innerHTML = '<option value="">Выберите файл</option>';
    
    // Add files to selectors
    nonDuplicateFiles.forEach(file => {
        const option1 = document.createElement('option');
        const option2 = document.createElement('option');
        
        option1.value = file.id;
        option1.textContent = file.name;
        option2.value = file.id;
        option2.textContent = file.name;
        
        file1Select.appendChild(option1);
        file2Select.appendChild(option2);
    });
}

// Compare Files
async function compareFiles() {
    const file1Id = file1Select.value;
    const file2Id = file2Select.value;
    
    if (!file1Id || !file2Id) {
        showToast('Выберите оба файла для сравнения', 'warning');
        return;
    }
    
    if (file1Id === file2Id) {
        showToast('Выберите разные файлы для сравнения', 'warning');
        return;
    }
    
    showLoadingOverlay(true);
    
    try {
        const response = await fetch(`${API_BASE}/compare`, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            },
            body: JSON.stringify({
                file_id: file1Id,
                other_file_id: file2Id
            })
        });
        
        const result = await response.json();
        
        if (response.ok) {
            displayComparisonResults(result, file1Id, file2Id);
            showToast('Сравнение завершено!', 'success');
        } else {
            throw new Error(result.error || 'Ошибка сравнения файлов');
        }
    } catch (error) {
        console.error('Comparison error:', error);
        showToast(`Ошибка: ${error.message}`, 'error');
    } finally {
        showLoadingOverlay(false);
    }
}

// Display Comparison Results
function displayComparisonResults(result, file1Id, file2Id) {
    const file1 = uploadedFiles.find(f => f.id === file1Id);
    const file2 = uploadedFiles.find(f => f.id === file2Id);
    
    const similarityPercentage = Math.round(result.jaccard_similarity * 100);
    
    comparisonResults.innerHTML = `
        <div class="comparison-header">
            <div class="similarity-score">${similarityPercentage}%</div>
            <div class="similarity-label">Схожесть по Жаккару</div>
            <div style="margin-top: 1rem;">
                <span class="file-badge ${result.identical ? 'success' : ''}">
                    ${result.identical ? 'Идентичные файлы' : 'Различающиеся файлы'}
                </span>
            </div>
        </div>
        <div class="comparison-details">
            <div>
                <h4>${file1.name}</h4>
                <div class="file-stats">
                    <div class="stat-item">
                        <div class="stat-value">${file1.stats.paragraphs}</div>
                        <div class="stat-label">Абзацы</div>
                    </div>
                    <div class="stat-item">
                        <div class="stat-value">${file1.stats.words}</div>
                        <div class="stat-label">Слова</div>
                    </div>
                    <div class="stat-item">
                        <div class="stat-value">${file1.stats.chars}</div>
                        <div class="stat-label">Символы</div>
                    </div>
                </div>
            </div>
            <div>
                <h4>${file2.name}</h4>
                <div class="file-stats">
                    <div class="stat-item">
                        <div class="stat-value">${file2.stats.paragraphs}</div>
                        <div class="stat-label">Абзацы</div>
                    </div>
                    <div class="stat-item">
                        <div class="stat-value">${file2.stats.words}</div>
                        <div class="stat-label">Слова</div>
                    </div>
                    <div class="stat-item">
                        <div class="stat-value">${file2.stats.chars}</div>
                        <div class="stat-label">Символы</div>
                    </div>
                </div>
            </div>
        </div>
    `;
    
    comparisonResults.style.display = 'block';
    comparisonResults.scrollIntoView({ behavior: 'smooth' });
}

// Show Toast Notification
function showToast(message, type = 'info') {
    const toast = document.createElement('div');
    toast.className = `toast ${type}`;
    toast.innerHTML = `
        <div style="display: flex; align-items: center; gap: 0.5rem;">
            <i class="fas fa-${getToastIcon(type)}"></i>
            <span>${message}</span>
        </div>
    `;
    
    document.getElementById('toastContainer').appendChild(toast);
    
    // Auto remove after 5 seconds
    setTimeout(() => {
        if (toast.parentNode) {
            toast.parentNode.removeChild(toast);
        }
    }, 5000);
    
    // Remove on click
    toast.addEventListener('click', () => {
        if (toast.parentNode) {
            toast.parentNode.removeChild(toast);
        }
    });
}

// Get Toast Icon
function getToastIcon(type) {
    switch (type) {
        case 'success': return 'check-circle';
        case 'error': return 'exclamation-circle';
        case 'warning': return 'exclamation-triangle';
        default: return 'info-circle';
    }
}

// Show/Hide Loading Overlay
function showLoadingOverlay(show) {
    loadingOverlay.style.display = show ? 'flex' : 'none';
}

// Local Storage Management
function saveToLocalStorage() {
    localStorage.setItem('text-scanner-files', JSON.stringify(uploadedFiles));
}

function loadFromLocalStorage() {
    const saved = localStorage.getItem('text-scanner-files');
    return saved ? JSON.parse(saved) : [];
}

// Load Uploaded Files on Page Load
function loadUploadedFiles() {
    uploadedFiles = loadFromLocalStorage();
    renderAllFiles();
    updateFileSelectors();
}

// Render All Files
function renderAllFiles() {
    filesGrid.innerHTML = '';
    if (uploadedFiles.length === 0) {
        filesGrid.innerHTML = `
            <div style="grid-column: 1 / -1; text-align: center; padding: 4rem; color: var(--text-secondary);">
                <i class="fas fa-file-alt" style="font-size: 4rem; margin-bottom: 1rem; opacity: 0.3;"></i>
                <h3>Файлы не найдены</h3>
                <p>Загрузите первый файл для начала анализа</p>
            </div>
        `;
    } else {
        uploadedFiles.forEach(file => renderFile(file));
    }
}

// Clear All Data (for admin use)
function clearAllData() {
    if (confirm('⚠️ ВНИМАНИЕ! Это удалит все данные из системы.\n\nВы уверены?')) {
        // Clear localStorage
        localStorage.removeItem('text-scanner-files');
        uploadedFiles = [];
        
        // Clear interface
        renderAllFiles();
        updateFileSelectors();
        
        // Hide comparison results
        comparisonResults.style.display = 'none';
        
        showToast('🗑️ Все данные очищены!', 'info');
        
        console.log('🧹 System cleared:', {
            localStorage: 'cleared',
            files: 'cleared',
            interface: 'reset'
        });
    }
}

// Add keyboard shortcut for admin clear (Ctrl+Shift+Delete)
document.addEventListener('keydown', function(e) {
    if (e.ctrlKey && e.shiftKey && e.key === 'Delete') {
        e.preventDefault();
        clearAllData();
    }
}); 