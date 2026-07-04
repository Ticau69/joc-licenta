using System;
using System.Threading.Tasks;
using UnityEngine;
using Firebase;
using Firebase.Auth;
using Firebase.Extensions;

public class AuthManager : MonoBehaviour
{
    private IEventBus _eventBus;
    private FirebaseAuth _auth;
    private bool _isFirebaseReady = false;

    public void Initialize(IEventBus eventBus)
    {
        _eventBus = eventBus;

        FirebaseApp.CheckAndFixDependenciesAsync().ContinueWithOnMainThread(task =>
        {
            try
            {
                var dependencyStatus = task.Result;
                if (dependencyStatus == DependencyStatus.Available)
                {
                    _auth = FirebaseAuth.DefaultInstance;
                    _isFirebaseReady = true;
                    Debug.Log("[AuthManager] Firebase a fost inițializat cu succes!");
                }
                else
                {
                    Debug.LogError($"[AuthManager] Nu s-au putut rezolva dependențele Firebase: {dependencyStatus}");
                    _eventBus?.Publish(new AuthFailedEvent
                    {
                        ErrorMessage = "Serviciile Google Play nu sunt disponibile pe acest dispozitiv."
                    });
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[AuthManager] Excepție la verificarea dependențelor Firebase: {ex.Message}");
                _eventBus?.Publish(new AuthFailedEvent
                {
                    ErrorMessage = "Eroare la inițializarea sistemului de autentificare."
                });
            }
        });
    }

    // Adăugăm parametrul 'username'
    public async Task RegisterWithEmailAsync(string email, string password, string username)
    {
        try
        {
            var authResult = await FirebaseAuth.DefaultInstance.CreateUserWithEmailAndPasswordAsync(email, password);
            Firebase.Auth.FirebaseUser newUser = authResult.User;

            if (newUser != null && !string.IsNullOrEmpty(username))
            {
                Firebase.Auth.UserProfile profile = new Firebase.Auth.UserProfile
                {
                    DisplayName = username
                };
                await newUser.UpdateUserProfileAsync(profile);
                Debug.Log($"[AuthManager] Nume profil setat cu succes: {newUser.DisplayName}");
            }
        }
        catch (Exception ex)
        {
            string mesajPrietenos = ParseFirebaseError(ex.Message);
            Debug.LogError($"[AuthManager] Înregistrare eșuată: {ex.Message}");

            _eventBus?.Publish(new AuthFailedEvent
            {
                ErrorMessage = mesajPrietenos
            });
        }
    }

    public async Task LoginWithEmailAsync(string email, string password)
    {
        Debug.Log($"--- [AUTH TRACE 1] Butonul de Login a fost apăsat pentru emailul: {email} ---");

        if (!_isFirebaseReady)
        {
            Debug.LogError("[AUTH TRACE EROARE] Firebase nu este pregătit! (Nu a rulat CheckAndFixDependenciesAsync)");
            return;
        }

        try
        {
            Debug.Log("[AUTH TRACE 2] Trimitem datele către serverele Google...");

            // Apelăm serverul Google pentru validare cont
            var authResult = await _auth.SignInWithEmailAndPasswordAsync(email, password);
            string userId = authResult.User.UserId;
            string username = email.Split('@')[0]; // Nume fallback

            Debug.Log($"[AUTH TRACE 3] Google a confirmat logarea! UserID generat: {userId}");

            // Aici e momentul critic! Verificăm dacă AuthManager are acces la magistrală
            if (_eventBus != null)
            {
                Debug.Log("[AUTH TRACE 4] Avem EventBus! Publicăm UserAuthenticatedEvent...");
                _eventBus.Publish(new UserAuthenticatedEvent
                {
                    UserId = userId,
                    Username = username
                });
            }
            else
            {
                Debug.LogError("[AUTH TRACE EROARE CRITICĂ] _eventBus este NULL în AuthManager! Funcția Initialize() nu a fost apelată corect la pornirea scenei, deci GameManager nu are cum să audă logarea!");
            }
        }
        catch (Exception ex)
        {
            string mesajPrietenos = ParseFirebaseError(ex.Message);
            Debug.LogError($"[AUTH TRACE EROARE FIREBASE] Logare eșuată: {ex.Message}");

            _eventBus?.Publish(new AuthFailedEvent
            {
                ErrorMessage = mesajPrietenos
            });
        }
    }

    private string ParseFirebaseError(string rawMessage)
    {
        if (rawMessage.Contains("WrongPassword") || rawMessage.Contains("INVALID_LOGIN_CREDENTIALS"))
            return "Parola introdusă este incorectă.";
        if (rawMessage.Contains("UserNotFound") || rawMessage.Contains("user not found"))
            return "Nu există un cont asociat acestui email.";
        if (rawMessage.Contains("EmailAlreadyInUse") || rawMessage.Contains("email address is already in use"))
            return "Acest email este deja utilizat de un alt cont.";
        if (rawMessage.Contains("weak password"))
            return "Parola este prea slabă (minim 6 caractere).";

        return "Eroare de conexiune la serverul de autentificare.";
    }
}