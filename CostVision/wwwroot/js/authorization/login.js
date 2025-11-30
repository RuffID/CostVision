document.addEventListener("DOMContentLoaded", initLoginPage);

function initLoginPage() {
    var form = document.getElementById("loginForm");
    if (!form) {
        return;
    }

    // Повесить обработчик отправки формы
    form.addEventListener("submit", handleLoginSubmit);
}

function handleLoginSubmit(event) {
    event.preventDefault();

    var errorBlock = document.getElementById("loginError");
    var loginButton = document.getElementById("loginButton");
    var loginInput = document.getElementById("Login");
    var passwordInput = document.getElementById("Password");

    if (!errorBlock || !loginButton || !loginInput || !passwordInput) {
        console.error("Элементы формы логина не найдены.");
        return;
    }

    errorBlock.textContent = "";
    loginButton.disabled = true;

    var login = loginInput.value;
    var password = passwordInput.value;

    sendLoginRequest(login, password, errorBlock, loginButton);
}

async function sendLoginRequest(login, password, errorBlock, loginButton) {
    try {
        var response = await fetch("/api/login", {
            method: "POST",
            headers: {
                "Content-Type": "application/json"
            },
            credentials: "include",
            body: JSON.stringify({
                login: login,
                password: password
            })
        });

        if (response.ok) {
            // Выполнить переход после успешного входа
            window.location.href = "/";
            return;
        }

        if (response.status === 400 || response.status === 401) {
            var json = null;
            try {
                json = await response.json();
            } catch (_) {
                // Пропустить парсинг тела при ошибке
            }

            if (json && json.error) {
                errorBlock.textContent = json.error;
            } else {
                errorBlock.textContent = "Неверный логин или пароль.";
            }
        } else {
            errorBlock.textContent = "Ошибка при входе. Попробуй позже.";
        }
    } catch (err) {
        console.error(err);
        errorBlock.textContent = "Ошибка сети.";
    } finally {
        if (loginButton) {
            loginButton.disabled = false;
        }
    }
}

