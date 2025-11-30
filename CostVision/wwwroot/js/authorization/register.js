// Инициализировать страницу после загрузки DOM
document.addEventListener("DOMContentLoaded", initRegisterPage);

function initRegisterPage() {
    var form = document.getElementById("registerForm");
    if (!form) {
        return;
    }

    form.addEventListener("submit", handleRegisterSubmit);
}

function handleRegisterSubmit(event) {
    // Остановить отправку формы
    event.preventDefault();

    var errorBlock = document.getElementById("registerError");
    var successBlock = document.getElementById("registerSuccess");
    var registerButton = document.getElementById("registerButton");
    var loginInput = document.getElementById("Login");
    var nameInput = document.getElementById("Name");
    var passwordInput = document.getElementById("Password");
    var confirmPasswordInput = document.getElementById("ConfirmPassword");

    if (!errorBlock || !successBlock || !registerButton ||
        !loginInput || !nameInput || !passwordInput || !confirmPasswordInput) {
        // Вывести ошибку при отсутствии нужных элементов
        console.error("Элементы формы регистрации не найдены.");
        return;
    }

    errorBlock.textContent = "";
    successBlock.textContent = "";
    registerButton.disabled = true;

    var login = loginInput.value;
    var name = nameInput.value;
    var password = passwordInput.value;
    var confirmPassword = confirmPasswordInput.value;

    if (!login || !name || !password || !confirmPassword) {
        // Показать ошибку валидации полей
        errorBlock.textContent = "Заполни все поля.";
        registerButton.disabled = false;
        return;
    }

    if (password !== confirmPassword) {
        // Показать ошибку несовпадения паролей
        errorBlock.textContent = "Пароль и подтверждение не совпадают.";
        registerButton.disabled = false;
        return;
    }

    var roleCheckboxes = document.querySelectorAll("input[name='role']:checked");
    if (!roleCheckboxes || roleCheckboxes.length === 0) {
        errorBlock.textContent = "Выбери хотя бы одну роль.";
        registerButton.disabled = false;
        return;
    }

    var roles = [];
    roleCheckboxes.forEach(function (checkbox) {
        var roleId = checkbox.value;
        var roleName = checkbox.getAttribute("data-role-name");
        roles.push({
            id: roleId,
            name: roleName
        });
    });

    sendRegisterRequest(login, name, password, roles, errorBlock, successBlock, registerButton);
}

async function sendRegisterRequest(login, name, password, roles, errorBlock, successBlock, registerButton) {
    try {
        var response = await fetch("/api/registration", {
            method: "POST",
            headers: {
                "Content-Type": "application/json"
            },
            // Отправить/принять куки
            credentials: "include",
            body: JSON.stringify({
                login: login,
                name: name,
                password: password,
                roles: roles
            })
        });

        if (response.ok) {
            // Показать успешную регистрацию
            successBlock.textContent = "Пользователь успешно зарегистрирован.";
            // Перенаправить на страницу входа
            window.location.href = "/";
            return;
        }

        if (response.status === 400 || response.status === 409) {
            var json = null;
            try {
                json = await response.json();
            } catch (_) {
                // Пропустить ошибку парсинга тела ответа
            }

            if (json && json.error) {
                errorBlock.textContent = json.error;
            } else {
                errorBlock.textContent = "Ошибка при регистрации.";
            }
        } else {
            errorBlock.textContent = "Неизвестная ошибка при регистрации.";
        }
    } catch (err) {
        console.error(err);
        errorBlock.textContent = "Ошибка сети.";
    } finally {
        if (registerButton) {
            registerButton.disabled = false;
        }
    }
}