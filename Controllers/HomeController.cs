using System.Diagnostics;
using System.Globalization;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using TP06.Models;

namespace TP06.Controllers;

public class HomeController : Controller
{
    private const string ProgressKey = "escapeProgress";
    private const string WinKey = "escapeWon";
    private const string IntroVideoPath = "/videos/Soccer_player_entering_prison_202609090817.mp4";
    private const string AcunaIntroVideoPath = "/videos/Soccer_players_running_in_tunnel_20260910090754.mp4";
    private const string GameOverVideoPath = "/videos/Soccer_players_running_in_tunnel_20260910090754.mp4";
    private static readonly string[] ChallengeOrder =
    {
        "Coudet",
        "Acuna",
        "Demichelis",
        "Tapia",
        "Scaloni",
        "Donofrio",
        "Di Carlo"
    };
    private readonly ILogger<HomeController> _logger;
    private readonly BD _bd;

    public HomeController(ILogger<HomeController> logger, IConfiguration configuration)
    {
        _logger = logger;
        _bd = new BD(configuration);
    }

    public IActionResult Index()
    {
        var progress = GetProgress();
        ViewBag.Progress = progress;
        ViewBag.CompletedCount = progress.Count;
        ViewBag.Won = IsWon();
        ViewBag.Participante = GetParticipanteActual();
        ViewBag.HabitacionActual = "Lobby";
        ViewBag.PartidaEnCurso = progress.Count > 0 || IsWon();
        return View();
    }

    public IActionResult IngresarCodigo()
    {
        ViewBag.Progress = GetProgress();
        ViewBag.CompletedCount = GetProgress().Count;
        ViewBag.Won = IsWon();
        ViewBag.Participante = GetParticipanteActual();
        ViewBag.HabitacionActual = "Lobby";
        ViewBag.PartidaEnCurso = GetProgress().Count > 0 || IsWon();
        return View();
    }

    [HttpPost]
    public IActionResult IngresarCodigos(string codigoSala, string codigoSesion)
    {
        if (string.IsNullOrWhiteSpace(codigoSala) || string.IsNullOrWhiteSpace(codigoSesion))
        {
            TempData["mensajeError"] = "Por favor ingresá los dos códigos (sala y sesión).";
            return RedirectToAction(nameof(IngresarCodigo));
        }

        var nombreIngresado = Request.Form["nombreParticipante"].ToString();
        if (!string.IsNullOrWhiteSpace(nombreIngresado))
        {
            nombreIngresado = nombreIngresado.Trim();
            HttpContext.Session.SetString("participante", nombreIngresado);
            _bd.GuardarParticipante(nombreIngresado);
        }

        var codigoSalaNormalizado = codigoSala.Trim();
        var codigoSesionNormalizado = codigoSesion.Trim();

        var salaId = _bd.ObtenerSalaIdPorCodigo(codigoSalaNormalizado);
        if (!salaId.HasValue)
        {
            salaId = ObtenerSalaIdPorCodigoFallback(codigoSalaNormalizado);
        }

        if (!salaId.HasValue)
        {
            var codigoSalaActual = HttpContext.Session.GetString("codigoSalaActual");
            if (!string.IsNullOrWhiteSpace(codigoSalaActual) &&
                string.Equals(codigoSalaActual.Trim(), codigoSalaNormalizado, StringComparison.OrdinalIgnoreCase))
            {
                salaId = ObtenerSalaIdPorCodigoFallback(codigoSalaActual.Trim());
            }
        }

        if (!salaId.HasValue)
        {
            TempData["mensajeError"] = "Código de sala inválido.";
            return RedirectToAction(nameof(IngresarCodigo));
        }

        var resultado = _bd.RecuperarProgresoPorCodigo(codigoSesionNormalizado);
        if (resultado == null)
        {
            var codigoActual = HttpContext.Session.GetString("codigoActual");
            if (!string.IsNullOrWhiteSpace(codigoActual) &&
                string.Equals(codigoActual.Trim(), codigoSesionNormalizado, StringComparison.OrdinalIgnoreCase))
            {
                var nombreParticipanteActual = GetParticipanteActual();
                var progresoActual = GetProgress();
                var tiempoRestanteActual = HttpContext.Session.GetInt32("tiempoRestanteSegundos") ?? 1800;
                resultado = (nombreParticipanteActual, string.Join(',', progresoActual), tiempoRestanteActual);
            }
        }

        if (resultado == null)
        {
            TempData["mensajeError"] = "Código de sesión inválido o expirado.";
            return RedirectToAction(nameof(IngresarCodigo));
        }

        var (nombreParticipante, progreso, tiempoRestante) = resultado.Value;
        var progresoRestaurado = string.IsNullOrWhiteSpace(progreso)
            ? GetChallengeNameBySalaId(salaId.Value)
            : progreso;

        HttpContext.Session.SetString("participante", nombreParticipante);
        HttpContext.Session.SetString(ProgressKey, progresoRestaurado ?? string.Empty);
        HttpContext.Session.SetString("codigoActual", codigoSesionNormalizado);
        HttpContext.Session.SetString("codigoSalaActual", codigoSalaNormalizado);
        HttpContext.Session.SetInt32("tiempoRestanteSegundos", tiempoRestante);

        try
        {
            _bd.RegistrarSalaActual(nombreParticipante, salaId.Value);
        }
        catch
        {
            // no crítico, continuar de todas formas
        }

        var dest = salaId.Value switch
        {
            1 => nameof(Coudet),
            2 => nameof(Acuna),
            3 => nameof(Demichelis),
            4 => nameof(Tapia),
            5 => nameof(Scaloni),
            6 => nameof(Donofrio),
            7 => nameof(DiCarlo),
            _ => nameof(Index)
        };

        if (string.Equals(dest, nameof(Coudet), StringComparison.OrdinalIgnoreCase))
        {
            TempData["ShowIntro"] = IntroVideoPath;
        }
        else if (string.Equals(dest, nameof(Acuna), StringComparison.OrdinalIgnoreCase))
        {
            TempData["ShowIntro"] = AcunaIntroVideoPath;
        }

        TempData["mensajeExito"] = $"Código válido. Bienvenido, {nombreParticipante}! Redirigiendo a la sala.";
        return RedirectToAction(dest);
    }

    [HttpPost]
    public IActionResult CargarProgreso(string codigoSesion)
    {
        if (string.IsNullOrWhiteSpace(codigoSesion))
        {
            TempData["mensajeError"] = "Por favor ingresá un código válido.";
            return RedirectToAction(nameof(Index));
        }

        var resultado = _bd.RecuperarProgresoPorCodigo(codigoSesion.Trim());
        if (resultado == null)
        {
            TempData["mensajeError"] = "El código no es válido o ha expirado.";
            return RedirectToAction(nameof(Index));
        }

        var (nombreParticipante, progreso, tiempoRestante) = resultado.Value;

        HttpContext.Session.SetString("participante", nombreParticipante);
        HttpContext.Session.SetString(ProgressKey, progreso);
        HttpContext.Session.SetString("codigoActual", codigoSesion.Trim());
        HttpContext.Session.SetInt32("tiempoRestanteSegundos", tiempoRestante);

        TempData["mensajeExito"] = $"Bienvenido de vuelta, {nombreParticipante}! Tu progreso ha sido restaurado.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public IActionResult Continuar(string codigo)
    {
        if (string.IsNullOrWhiteSpace(codigo))
        {
            return RedirectToAction(nameof(Index));
        }

        var normalized = codigo.Trim();

        var dest = normalized.ToUpperInvariant() switch
        {
            "CANTILO" => nameof(Coudet),
            "VESTUARIO" => nameof(Acuna),
            "JETLAG" => nameof(Demichelis),
            "FIGURA" => nameof(Tapia),
            "SECUENCIA" => nameof(Scaloni),
            "EQUIPO" => nameof(Donofrio),
            "RIVER" => nameof(DiCarlo),
            _ => nameof(Index)
        };

        // show intro video for specific rooms
        if (string.Equals(dest, nameof(Coudet), StringComparison.OrdinalIgnoreCase))
        {
            TempData["ShowIntro"] = IntroVideoPath;
        }
        else if (string.Equals(dest, nameof(Acuna), StringComparison.OrdinalIgnoreCase))
        {
            TempData["ShowIntro"] = AcunaIntroVideoPath;
        }

        return RedirectToAction(dest);
    }

    public IActionResult Coudet()
    {
        if (!CanAccessChallenge("Coudet"))
        {
            return RedirectToAction(nameof(Index));
        }

        PrepareRoomView(nameof(Coudet));

        var tiempoRestante = HttpContext.Session.GetInt32("tiempoRestanteSegundos") ?? 1800;
        if (tiempoRestante <= 0)
        {
            LimpiarSesion();
            TempData["mensajeError"] = "Se acabó el tiempo. La partida finalizó.";
            return RedirectToAction(nameof(Index));
        }
        HttpContext.Session.SetInt32("tiempoRestanteSegundos", tiempoRestante);

        var attempts = HttpContext.Session.GetInt32("coudetAttempts") ?? 0;
        var dice = HttpContext.Session.GetString("coudetDice") ?? string.Empty;
        var held = HttpContext.Session.GetString("coudetHeld") ?? string.Empty;

        if (string.IsNullOrWhiteSpace(dice))
        {
            var random = new Random();
            var initial = Enumerable.Range(1, 5).Select(_ => random.Next(1, 7)).ToArray();
            HttpContext.Session.SetString("coudetDice", string.Join(',', initial));
            dice = string.Join(',', initial);
        }

        var sessionKey = GetIntroSessionKey(nameof(Coudet));
        var shouldShowIntro = string.Equals(HttpContext.Session.GetString(sessionKey), "true", StringComparison.OrdinalIgnoreCase) ? false : true;
        if (shouldShowIntro)
        {
            HttpContext.Session.SetString(sessionKey, "true");
            ViewBag.IntroVideo = IntroVideoPath;
        }
        else
        {
            ViewBag.IntroVideo = null;
        }

        ViewBag.Mensaje = TempData["mensaje"];
        ViewBag.Correcto = TempData["correcto"];
        ViewBag.Progress = GetProgress();
        ViewBag.Participante = GetParticipanteActual();
        ViewBag.HabitacionActual = "Coudet";
        ViewBag.PartidaEnCurso = true;
        ViewBag.Attempts = attempts;
        ViewBag.Dice = dice;
        ViewBag.Held = held;
        ViewBag.TiempoRestanteSegundos = tiempoRestante;
        return View();
    }

    [HttpPost]
    public IActionResult Coudet(string accion, string dados, string held, int intentos, int? tiempoRestanteSegundos = null)
    {
        if (tiempoRestanteSegundos.HasValue)
        {
            ActualizarTiempoDelFormulario(tiempoRestanteSegundos.Value);
        }

        if (!CanAccessChallenge("Coudet"))
        {
            return RedirectToAction(nameof(Index));
        }

        var attempts = Math.Max(0, intentos);
        HttpContext.Session.SetInt32("coudetAttempts", attempts);

        var diceValues = ParseDiceValues(dados);
        if (diceValues.Length != 5)
        {
            diceValues = ParseDiceValues(HttpContext.Session.GetString("coudetDice") ?? string.Empty);
        }

        var diceText = string.Join(", ", diceValues);
        HttpContext.Session.SetString("coudetDice", diceText);
        HttpContext.Session.SetString("coudetHeld", held ?? string.Empty);

        if (attempts >= 100)
        {
            if (diceValues.Length != 5 || !diceValues.All(value => value == diceValues[0]))
            {
                var forcedValue = diceValues.Length > 0 ? diceValues[0] : 6;
                diceValues = Enumerable.Repeat(forcedValue, 5).ToArray();
                diceText = string.Join(", ", diceValues);
                HttpContext.Session.SetString("coudetDice", diceText);
                HttpContext.Session.SetString("coudetHeld", string.Empty);
                TempData["mensaje"] = "Llegaste al intento 100. Los dados quedaron iguales para que puedas cerrar la sala.";
                TempData["correcto"] = false;
                return RedirectToAction(nameof(Coudet));
            }
        }

        if (attempts <= 0)
        {
            TempData["mensaje"] = "Primero tenés que tirar los dados para intentar la generala.";
            TempData["correcto"] = false;
            return RedirectToAction(nameof(Coudet));
        }

        var isCorrect = diceValues.Length == 5 && diceValues.All(value => value == diceValues[0]);

        if (isCorrect)
        {
            SaveProgress("Coudet");
            HttpContext.Session.Remove("coudetAttempts");
            HttpContext.Session.Remove("coudetDice");
            HttpContext.Session.Remove("coudetHeld");
            TempData["mensaje"] = "Correcto: tiraste la generala y abriste la salida de la primera sala.";
            TempData["correcto"] = true;
            return RedirectToNextChallenge();
        }

        if (attempts >= 100)
        {
            TempData["mensaje"] = "Llegaste al intento 100 y todavía no tenías la generala. Te obligaron a repetir desde el principio.";
            TempData["correcto"] = false;
            HttpContext.Session.Remove("coudetAttempts");
            HttpContext.Session.Remove("coudetDice");
            HttpContext.Session.Remove("coudetHeld");
            return RedirectToAction(nameof(Coudet));
        }

        TempData["mensaje"] = $"Tirada {attempts}: {diceText}. Los dados todavía no coinciden. Seguí tirando.";
        TempData["correcto"] = false;
        return RedirectToAction(nameof(Coudet));
    }

    private static int[] ParseDiceValues(string dados)
    {
        if (string.IsNullOrWhiteSpace(dados))
        {
            return Array.Empty<int>();
        }

        return dados
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(part => int.TryParse(part, out var value) ? value : 0)
            .Where(value => value >= 1 && value <= 6)
            .ToArray();
    }

    public IActionResult Acuna()
    {
        if (!CanAccessChallenge("Acuna"))
        {
            return RedirectToAction(nameof(Index));
        }

        PrepareRoomView("Acuna");

        var tiempoRestante = HttpContext.Session.GetInt32("tiempoRestanteSegundos") ?? 1800;
        if (tiempoRestante <= 0)
        {
            LimpiarSesion();
            TempData["mensajeError"] = "Se acabó el tiempo. La partida finalizo.";
            return RedirectToAction(nameof(Index));
        }

        var sessionKey = GetIntroSessionKey(nameof(Acuna));
        var shouldShowIntro = string.Equals(HttpContext.Session.GetString(sessionKey), "true", StringComparison.OrdinalIgnoreCase) ? false : true;
        if (shouldShowIntro)
        {
            HttpContext.Session.SetString(sessionKey, "true");
            ViewBag.IntroVideo = AcunaIntroVideoPath;
        }
        else
        {
            ViewBag.IntroVideo = null;
        }

        ViewBag.Correcto = TempData["correcto"];
        ViewBag.Progress = GetProgress();
        ViewBag.Participante = GetParticipanteActual();
        ViewBag.HabitacionActual = "Acuna";
        ViewBag.PartidaEnCurso = true;
        ViewBag.TiempoRestanteSegundos = tiempoRestante;
        return View();
    }

    [HttpPost]
    public IActionResult Acuna(string[] objetos)
    {
        if (!CanAccessChallenge("Acuna"))
        {
            return RedirectToAction(nameof(Index));
        }

        var selected = objetos ?? Array.Empty<string>();
        var expected = new[] { "gorro", "remera", "mochila" };
        var isCorrect = selected.Length == expected.Length && expected.All(item => selected.Contains(item, StringComparer.OrdinalIgnoreCase));

        if (isCorrect)
        {
            SaveProgress("Acuna");
            TempData["mensaje"] = "Perfecto: armaste el vestuario 360° y encontraste los objetos clave.";
            TempData["correcto"] = true;
            return RedirectToNextChallenge();
        }
        else
        {
            TempData["mensaje"] = "Todavía falta algo en el vestuario. Revisá la lista de prendas y objetos.";
            TempData["correcto"] = false;
        }

        return RedirectToAction(nameof(Acuna));
    }

    [HttpPost]
    public IActionResult AdvanceFromAcuna(string[] objetos)
    {
        if (!CanAccessChallenge("Acuna"))
        {
            return RedirectToAction(nameof(Index));
        }

        // mark Acuna as completed and go directly to Demichelis
        SaveProgress("Acuna");
        TempData["mensaje"] = "Perfecto: armaste el vestuario 360° y encontraste los objetos clave.";
        TempData["correcto"] = true;
        return RedirectToNextChallenge();
    }

    public IActionResult Demichelis()
    {
        if (!CanAccessChallenge("Demichelis"))
        {
            return RedirectToAction(nameof(Index));
        }

        PrepareRoomView("Demichelis");

        var tiempoRestante = HttpContext.Session.GetInt32("tiempoRestanteSegundos") ?? 1800;
        if (tiempoRestante <= 0)
        {
            LimpiarSesion();
            TempData["mensajeError"] = "Se acabó el tiempo. La partida finalizo.";
            return RedirectToAction(nameof(Index));
        }

        ViewBag.Mensaje = TempData["mensaje"];
        ViewBag.Correcto = TempData["correcto"];
        ViewBag.Progress = GetProgress();
        ViewBag.Participante = GetParticipanteActual();
        ViewBag.HabitacionActual = "Demichelis";
        ViewBag.PartidaEnCurso = true;
        ViewBag.TiempoRestanteSegundos = tiempoRestante;
        return View();
    }

    [HttpPost]
    public IActionResult Demichelis(string respuesta)
    {
        if (!CanAccessChallenge("Demichelis"))
        {
            return RedirectToAction(nameof(Index));
        }

        var palabra1 = Request.Form["palabra1"].ToString();
        var palabra2 = Request.Form["palabra2"].ToString();
        var palabra3 = Request.Form["palabra3"].ToString();

        var respuestaCombinada = string.Join(" ", new[] { palabra1, palabra2, palabra3 }
            .Select(p => p?.Trim())
            .Where(p => !string.IsNullOrWhiteSpace(p)));

        if (string.IsNullOrWhiteSpace(respuestaCombinada))
        {
            respuestaCombinada = respuesta ?? string.Empty;
        }

        // Build expected phrase from the database table Demichelis
        var palabras = _bd.ObtenerPalabrasDemichelis();
        var expected = string.Join(" ", palabras)
            .Replace("\r", " ")
            .Replace("\n", " ");
        expected = string.Join(" ", expected.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries));

        var isCorrect = string.Equals(respuestaCombinada.Trim(), expected.Trim(), StringComparison.OrdinalIgnoreCase);

        if (isCorrect)
        {
            SaveProgress("Demichelis");
            TempData["mensaje"] = "Excelente: decodificaste el mensaje del jet-lag y se abre la siguiente salida.";
            TempData["correcto"] = true;
            return RedirectToNextChallenge();
        }
        else
        {
            TempData["mensaje"] = "El audio estaba en alemán pero la clave está en el diccionario. Releé la frase.";
            TempData["correcto"] = false;
        }

        return RedirectToAction(nameof(Demichelis));
    }

    public IActionResult Tapia()
    {
        if (!CanAccessChallenge("Tapia"))
        {
            return RedirectToAction(nameof(Index));
        }

        PrepareRoomView("Tapia");

        var tiempoRestante = HttpContext.Session.GetInt32("tiempoRestanteSegundos") ?? 1800;
        if (tiempoRestante <= 0)
        {
            LimpiarSesion();
            TempData["mensajeError"] = "Se acabó el tiempo. La partida finalizo.";
            return RedirectToAction(nameof(Index));
        }

        ViewBag.Mensaje = TempData["mensaje"];
        ViewBag.Correcto = TempData["correcto"];
        ViewBag.Progress = GetProgress();
        ViewBag.Participante = GetParticipanteActual();
        ViewBag.HabitacionActual = "Tapia";
        ViewBag.PartidaEnCurso = true;
        ViewBag.TiempoRestanteSegundos = tiempoRestante;
        return View();
    }

    [HttpPost]
    public IActionResult Tapia(string respuesta)
    {
        if (!CanAccessChallenge("Tapia"))
        {
            return RedirectToAction(nameof(Index));
        }

        var isCorrect = string.Equals(respuesta?.Trim(), "CIRCULO-TRIANGULO-CUADRADO", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(respuesta?.Trim(), "CÍRCULO-TRIÁNGULO-CUADRADO", StringComparison.OrdinalIgnoreCase);

        if (isCorrect)
        {
            SaveProgress("Tapia");
            TempData["mensaje"] = "Bien: la figura de arriba coincide con el orden correcto de abajo.";
            TempData["correcto"] = true;
            return RedirectToNextChallenge();
        }
        else
        {
            TempData["mensaje"] = "La secuencia visual no coincide todavía. Revisá el orden de las figuras.";
            TempData["correcto"] = false;
        }

        return RedirectToAction(nameof(Tapia));
    }

    [HttpPost]
    public IActionResult TapiaComplete(int? tiempoSegundos = null)
    {
        if (tiempoSegundos.HasValue)
        {
            ActualizarTiempoDelFormulario(tiempoSegundos.Value);
        }

        if (!CanAccessChallenge("Tapia"))
        {
            return Json(new { redirect = Url.Action(nameof(Index)) });
        }

        SaveProgress("Tapia");
        TempData["mensaje"] = "Genial: resolviste la sala Tapia.";
        TempData["correcto"] = true;

        var progress = GetProgress();

        foreach (var challenge in ChallengeOrder)
        {
            if (!progress.Any(item => string.Equals(item, challenge, StringComparison.OrdinalIgnoreCase)))
            {
                var actionName = challenge switch
                {
                    "Coudet" => nameof(Coudet),
                    "Acuna" => nameof(Acuna),
                    "Demichelis" => nameof(Demichelis),
                    "Tapia" => nameof(Tapia),
                    "Scaloni" => nameof(Scaloni),
                    "Donofrio" => nameof(Donofrio),
                    "Di Carlo" => nameof(DiCarlo),
                    _ => nameof(Index)
                };

                return Json(new { redirect = Url.Action(actionName) });
            }
        }

        return Json(new { redirect = Url.Action(nameof(Ganaste)) });
    }

    public IActionResult Scaloni()
    {
        if (!CanAccessChallenge("Scaloni"))
        {
            return RedirectToAction(nameof(Index));
        }

        PrepareRoomView("Scaloni");

        var tiempoRestante = HttpContext.Session.GetInt32("tiempoRestanteSegundos") ?? 1800;
        if (tiempoRestante <= 0)
        {
            LimpiarSesion();
            TempData["mensajeError"] = "Se acabó el tiempo. La partida finalizo.";
            return RedirectToAction(nameof(Index));
        }

        ViewBag.Mensaje = TempData["mensaje"];
        ViewBag.Correcto = TempData["correcto"];
        ViewBag.Progress = GetProgress();
        ViewBag.Participante = GetParticipanteActual();
        ViewBag.HabitacionActual = "Scaloni";
        ViewBag.PartidaEnCurso = true;
        ViewBag.TiempoRestanteSegundos = tiempoRestante;
        return View();
    }

    [HttpPost]
    public IActionResult Scaloni(string respuesta)
    {
        if (!CanAccessChallenge("Scaloni"))
        {
            return RedirectToAction(nameof(Index));
        }

        var isCorrect = string.Equals(respuesta?.Trim(), "DERECHA-IZQUIERDA-DERECHA-CENTRO", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(respuesta?.Trim(), "D-I-D-C", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(respuesta?.Trim(), "D, I, D, C", StringComparison.OrdinalIgnoreCase);

        if (isCorrect)
        {
            SaveProgress("Scaloni");
            TempData["mensaje"] = "Correcto: recordaste bien el camino y desbloqueaste la ruta.";
            TempData["correcto"] = true;
            return RedirectToNextChallenge();
        }

        TempData["mensaje"] = "La secuencia no quedó grabada. Mirá otra vez el recorrido y repetilo con calma.";
        TempData["correcto"] = false;
        return RedirectToAction(nameof(Scaloni));
    }

    [HttpPost]
    public IActionResult ScaloniComplete(int? tiempoSegundos = null)
    {
        if (tiempoSegundos.HasValue)
        {
            ActualizarTiempoDelFormulario(tiempoSegundos.Value);
        }

        if (!CanAccessChallenge("Scaloni"))
        {
            return Json(new { redirect = Url.Action(nameof(Index)) });
        }

        SaveProgress("Scaloni");
        TempData["mensaje"] = "Perfecto: recordaste bien el camino y desbloqueaste la ruta.";
        TempData["correcto"] = true;

        var progress = GetProgress();

        foreach (var challenge in ChallengeOrder)
        {
            if (!progress.Any(item => string.Equals(item, challenge, StringComparison.OrdinalIgnoreCase)))
            {
                var actionName = challenge switch
                {
                    "Coudet" => nameof(Coudet),
                    "Acuna" => nameof(Acuna),
                    "Demichelis" => nameof(Demichelis),
                    "Tapia" => nameof(Tapia),
                    "Scaloni" => nameof(Scaloni),
                    "Donofrio" => nameof(Donofrio),
                    "Di Carlo" => nameof(DiCarlo),
                    _ => nameof(Index)
                };

                return Json(new { redirect = Url.Action(actionName) });
            }
        }

        return Json(new { redirect = Url.Action(nameof(Ganaste)) });
    }

    public IActionResult Donofrio()
    {
        if (!CanAccessChallenge("Donofrio"))
        {
            return RedirectToAction(nameof(Index));
        }

        PrepareRoomView("Donofrio");

        var tiempoRestante = HttpContext.Session.GetInt32("tiempoRestanteSegundos") ?? 1800;
        if (tiempoRestante <= 0)
        {
            LimpiarSesion();
            TempData["mensajeError"] = "Se acabó el tiempo. La partida finalizo.";
            return RedirectToAction(nameof(Index));
        }

        var jugadores = GetDonofrioJugadores();
        var plantilla = GetDonofrioPlantilla();
        var jugadoresAdivinados = jugadores
            .Where(j => plantilla.Contains(NormalizePlayerName(j.Nombre), StringComparer.OrdinalIgnoreCase))
            .OrderBy(j => j.Id)
            .ToList();

        var objetivo = jugadores
            .FirstOrDefault(j => !plantilla.Contains(NormalizePlayerName(j.Nombre), StringComparer.OrdinalIgnoreCase));

        var equipoCompleto = jugadoresAdivinados.Count == jugadores.Count;
        if (equipoCompleto)
        {
            SaveProgress("Donofrio");
            HttpContext.Session.SetString(WinKey, "true");
        }

        ViewBag.Mensaje = TempData["mensaje"];
        ViewBag.Correcto = TempData["correcto"];
        ViewBag.Progress = GetProgress();
        ViewBag.Participante = GetParticipanteActual();
        ViewBag.HabitacionActual = "Donofrio";
        ViewBag.PartidaEnCurso = true;
        ViewBag.Jugadores = jugadores;
        ViewBag.Plantilla = jugadoresAdivinados;
        ViewBag.Objetivo = objetivo;

        var tableroCompleto = BuildDonofrioBoard(jugadoresAdivinados);
        ViewBag.Arqueros = tableroCompleto.Where(s => s.Categoria == "Arquero").ToList();
        ViewBag.Defensores = tableroCompleto.Where(s => s.Categoria == "Defensor").ToList();
        ViewBag.Mediocampistas = tableroCompleto.Where(s => s.Categoria == "Mediocampista").ToList();
        ViewBag.Delanteros = tableroCompleto.Where(s => s.Categoria == "Delantero").ToList();
        ViewBag.EquipoCompleto = equipoCompleto;
        ViewBag.TiempoRestanteSegundos = tiempoRestante;
        return View();
    }

    [HttpPost]
    public IActionResult Donofrio(string respuesta, int? tiempoRestanteSegundos = null)
    {
        if (tiempoRestanteSegundos.HasValue)
        {
            ActualizarTiempoDelFormulario(tiempoRestanteSegundos.Value);
        }

        if (!CanAccessChallenge("Donofrio"))
        {
            return RedirectToAction(nameof(Index));
        }

        var jugadores = GetDonofrioJugadores();
        var plantilla = GetDonofrioPlantilla();

        if (string.IsNullOrWhiteSpace(respuesta))
        {
            TempData["mensaje"] = "Escribí el nombre del jugador que querés sumar a la formación.";
            TempData["correcto"] = false;
            return RedirectToAction(nameof(Donofrio));
        }

        var respuestaNormalizada = NormalizePlayerName(respuesta);
        var jugador = jugadores.FirstOrDefault(j => NormalizePlayerName(j.Nombre) == respuestaNormalizada);

        if (jugador == null)
        {
            TempData["mensaje"] = "Ese jugador no está en la lista del equipo. Revisá la pista y probá otra vez.";
            TempData["correcto"] = false;
            return RedirectToAction(nameof(Donofrio));
        }

        var nombreNormalizado = NormalizePlayerName(jugador.Nombre);
        if (plantilla.Contains(nombreNormalizado, StringComparer.OrdinalIgnoreCase))
        {
            TempData["mensaje"] = $"{jugador.Nombre} ya está en la formación.";
            TempData["correcto"] = false;
            return RedirectToAction(nameof(Donofrio));
        }

        plantilla.Add(nombreNormalizado);
        SetDonofrioPlantilla(plantilla);

        var equipoCompleto = plantilla.Count == jugadores.Count;
        if (equipoCompleto)
        {
            SaveProgress("Donofrio");
            HttpContext.Session.SetString(WinKey, "true");
            TempData["mensaje"] = "Formación completa: ya tenés armado el equipo para continuar con el escape.";
            TempData["correcto"] = true;
        }
        else
        {
            TempData["mensaje"] = $"Acertaste: {jugador.Nombre} quedó incorporado a la formación.";
            TempData["correcto"] = true;
        }

        return RedirectToAction(nameof(Donofrio));
    }

    private List<string> GetDonofrioPlantilla()
    {
        var raw = HttpContext.Session.GetString("donofrioPlantilla") ?? string.Empty;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return new List<string>();
        }

        return raw.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private void SetDonofrioPlantilla(IEnumerable<string> nombres)
    {
        var lista = nombres
            .Select(NormalizePlayerName)
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        HttpContext.Session.SetString("donofrioPlantilla", string.Join('|', lista));
    }

    private static List<JugadorDiCarlo> GetDonofrioJugadores()
    {
        return new List<JugadorDiCarlo>
        {
            new() { Id = 1, Nombre = "Armani", Adivinanza = "Bajo los tres palos parece gigante, con tapadas eternas en noches de gloria. En Madrid fue la muralla más imponente, ¿quién es el pulpo que entró en la historia?", Posicion = "Arquero" },
            new() { Id = 2, Nombre = "Montiel", Adivinanza = "Por el lateral derecho no pasa nadie y de penal tiene pulso helado de acero. Campeón de América y del mundo con el Millo, ¿quién puso el sello final al trofeo?", Posicion = "Defensor" },
            new() { Id = 3, Nombre = "Passarella", Adivinanza = "Defensor con gol y voz de mando, el Gran Capitán de temple de fierro. Cinta en el brazo, zurda letal, ¿quién dominaba todo el terreno?", Posicion = "Defensor" },
            new() { Id = 4, Nombre = "Maidana", Adivinanza = "Casi no habla pero deja la vida, una muralla que no conoce la piedad. Brazos firmes, fiera en cada cruce, ¿qué caudillo es el terror de la Boca?", Posicion = "Defensor" },
            new() { Id = 5, Nombre = "Acuña", Adivinanza = "Recién llegado pero con garra de campeón, un Huevo que traba en cada jugada. Por la banda izquierda va como un camión, ¿qué lateral se puso la banda cruzada?", Posicion = "Defensor" },
            new() { Id = 6, Nombre = "Enzo Pérez", Adivinanza = "Dejó la vida en el medio y fue leyenda, se puso el buzo verde lesionado y sin dudar. Atajó noventa minutos para la historia, ¿quién es el hincha que jugó en el altar?", Posicion = "Mediocampista" },
            new() { Id = 7, Nombre = "Ponzio", Adivinanza = "Comandante de mil batallas y finales, mordía en el medio sin aflojar la garra. Eterno capitán de la era más gloriosa, ¿quién es el León de la pampa?", Posicion = "Mediocampista" },
            new() { Id = 8, Nombre = "Enzo Fernandez", Adivinanza = "Surgido del club con pase y panorama, hizo cantar a todo el Monumental. De la cantera al mundo sin escalas, ¿quién la rompió antes de ir a Europa?", Posicion = "Mediocampista" },
            new() { Id = 9, Nombre = "Julian Alvarez", Adivinanza = "Picó sin parar tras cada pelota, goleador voraz, insaciable de gol. Le hizo seis a Alianza en una sola noche, ¿qué Araña nos dio tanta pasión?", Posicion = "Delantero" },
            new() { Id = 10, Nombre = "Francescoli", Adivinanza = "Elegancia pura con la banda en el pecho, un Príncipe charrúa de clase infinita. De chilena o de tiro libre un deleite, ¿quién es el ídolo que el hincha palpita?", Posicion = "Delantero" },
            new() { Id = 11, Nombre = "Falcao", Adivinanza = "Llegó de Colombia a gritar sus goles, un Tigre voraz adentro del área. Rugió en la red con cabezazos certeros, ¿quién dejó su huella con fuerza e hidalguía?", Posicion = "Delantero" }
        };
    }

    private static List<DonofrioSlot> BuildDonofrioBoard(List<JugadorDiCarlo> jugadoresAdivinados)
    {
        // Fila 1: Arquero (centrado)
        var arqueros = new[]
        {
            new DonofrioSlot { Label = "GK", Categoria = "Arquero", CssClass = "slot-gk", Fila = 1, Centrado = true }
        };

        // Fila 2: Defensores (4)
        var defensores = new[]
        {
            new DonofrioSlot { Label = "LB", Categoria = "Defensor", CssClass = "slot-lb", Fila = 2 },
            new DonofrioSlot { Label = "CB", Categoria = "Defensor", CssClass = "slot-cb-left", Fila = 2 },
            new DonofrioSlot { Label = "CB", Categoria = "Defensor", CssClass = "slot-cb-right", Fila = 2 },
            new DonofrioSlot { Label = "RB", Categoria = "Defensor", CssClass = "slot-rb", Fila = 2 }
        };

        // Fila 3: Mediocampistas
        var mediocampistas = new[]
        {
            new DonofrioSlot { Label = "CM", Categoria = "Mediocampista", CssClass = "slot-cm-left", Fila = 3 },
            new DonofrioSlot { Label = "CM", Categoria = "Mediocampista", CssClass = "slot-cm-right", Fila = 3 },
            new DonofrioSlot { Label = "CAM", Categoria = "Mediocampista", CssClass = "slot-cam", Fila = 3 }
        };

        // Fila 4: Delanteros
        var delanteros = new[]
        {
            new DonofrioSlot { Label = "LW", Categoria = "Delantero", CssClass = "slot-lw", Fila = 4 },
            new DonofrioSlot { Label = "ST", Categoria = "Delantero", CssClass = "slot-st", Fila = 4 },
            new DonofrioSlot { Label = "RW", Categoria = "Delantero", CssClass = "slot-rw", Fila = 4 }
        };

        var slots = arqueros.Concat(defensores).Concat(mediocampistas).Concat(delanteros).ToArray();

        var usados = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var slot in slots)
        {
            slot.Jugador = jugadoresAdivinados
                .FirstOrDefault(j =>
                    string.Equals(j.Posicion, slot.Categoria, StringComparison.OrdinalIgnoreCase)
                    && !usados.Contains(NormalizePlayerName(j.Nombre)));

            if (slot.Jugador != null)
            {
                usados.Add(NormalizePlayerName(slot.Jugador.Nombre));
            }
        }

        return slots.ToList();
    }

    // Público porque la vista Donofrio.cshtml castea el ViewBag a este tipo.
    public sealed class DonofrioSlot
    {
        public string Label { get; set; } = string.Empty;
        public string Categoria { get; set; } = string.Empty;
        public string CssClass { get; set; } = string.Empty;
        public JugadorDiCarlo? Jugador { get; set; }
        public int Fila { get; set; }
        public bool Centrado { get; set; }
    }

    public IActionResult DiCarlo()
    {
        if (!CanAccessChallenge("Di Carlo"))
        {
            return RedirectToAction(nameof(Index));
        }

        PrepareRoomView("Di Carlo");

        var tiempoRestante = HttpContext.Session.GetInt32("tiempoRestanteSegundos") ?? 1800;
        if (tiempoRestante <= 0)
        {
            LimpiarSesion();
            TempData["mensajeError"] = "Se acabó el tiempo. La partida finalizo.";
            return RedirectToAction(nameof(Index));
        }

        var state = GetDiCarloGameState();
        var escenaActual = GetDiCarloEscena(state.Paso);
        var equipo = GetEquipoDonofrioActual();

        ViewBag.Mensaje = TempData["mensaje"];
        ViewBag.Correcto = TempData["correcto"];
        ViewBag.Progress = GetProgress();
        ViewBag.Participante = GetParticipanteActual();
        ViewBag.HabitacionActual = "Di Carlo";
        ViewBag.PartidaEnCurso = true;
        ViewBag.TiempoRestanteSegundos = tiempoRestante;
        ViewBag.EquipoDonofrio = equipo;
        ViewBag.Escena = escenaActual;
        ViewBag.PartidoGanado = string.Equals(state.Estado, "ganado", StringComparison.OrdinalIgnoreCase);
        ViewBag.PartidoPerdido = string.Equals(state.Estado, "perdido", StringComparison.OrdinalIgnoreCase);
        ViewBag.DiCarloState = state;
        ViewBag.GolesArgentina = state.GolesArgentina;
        ViewBag.GolesRival = state.GolesRival;
        ViewBag.MinutoActual = escenaActual.Minuto;
        ViewBag.UltimoMinuto = state.UltimoMinuto;
        ViewBag.UltimoResultadoTipo = state.UltimoResultadoTipo;
        ViewBag.UltimoResultadoTexto = state.UltimoResultadoTexto;
        ViewBag.UltimoResultadoCorrecto = state.UltimoResultadoCorrecto;
        return View();
    }

    [HttpPost]
    public IActionResult DiCarlo(string decision)
    {
        if (!CanAccessChallenge("Di Carlo"))
        {
            return RedirectToAction(nameof(Index));
        }

        var state = GetDiCarloGameState();
        var escenaActual = GetDiCarloEscena(state.Paso);

        if (string.IsNullOrWhiteSpace(decision))
        {
            TempData["mensaje"] = "Elegí una opción para seguir jugando.";
            TempData["correcto"] = false;
            return RedirectToAction(nameof(DiCarlo));
        }

        var opcionElegida = NormalizeDecision(decision);
        var opcionCorrecta = NormalizeDecision(escenaActual.Correcta);
        var acierto = string.Equals(opcionElegida, opcionCorrecta, StringComparison.OrdinalIgnoreCase);

        if (acierto)
        {
            // Acertó: si era un ataque, suma gol; si era defensa, evita un gol
            if (escenaActual.EsAtaque)
            {
                state.GolesArgentina += 1;
            }
            TempData["mensaje"] = escenaActual.Exito;
            TempData["correcto"] = true;
        }
        else
        {
            // Falló: si era un ataque, no pasa nada especial; si era defensa, rival mete gol
            if (!escenaActual.EsAtaque)
            {
                state.GolesRival += 1;
            }
            TempData["mensaje"] = escenaActual.Fracaso;
            TempData["correcto"] = false;
        }

        // Guardamos qué pasó en esta jugada para mostrarlo en el tablero central
        state.UltimoMinuto = escenaActual.Minuto;
        state.UltimoResultadoTipo = acierto ? escenaActual.ResultadoExitoTipo : escenaActual.ResultadoFracasoTipo;
        state.UltimoResultadoTexto = acierto ? escenaActual.Exito : escenaActual.Fracaso;
        state.UltimoResultadoCorrecto = acierto;

        // Verificar si ganó (3 goles)
        if (state.GolesArgentina >= 3)
        {
            state.Estado = "ganado";
            SaveProgress("Di Carlo");
            HttpContext.Session.SetString(WinKey, "true");
            SetDiCarloGameState(state);
            TempData["mensaje"] = $"¡CANTILO GANA 3-{state.GolesRival}! Derrotaste a Los Robots de Di Carlo. ¡Escapás de la sala!";   
            TempData["correcto"] = true;
            return RedirectToAction(nameof(Ganaste));
        }

        // Verificar si perdió antes de llegar al final (rival 3 goles)
        if (state.GolesRival >= 3)
        {
            state.Estado = "perdido";
            SetDiCarloGameState(state);
            LimpiarSesion();
            TempData["mensajeError"] = $"Cantilo perdió {state.GolesRival}-{state.GolesArgentina} contra Los Robots de Di Carlo. La sala se cerró.";
            return RedirectToAction(nameof(Perdiste));
        }

        // Avanzar a la siguiente escena
        state.Paso += 1;
        state.UltimaDecision = decision;

        // Verificar si terminó el partido (todas las escenas jugadas)
        if (state.Paso >= GetDiCarloEscenas().Count)
        {
            // Si Cantilo tiene 3, ganó
            if (state.GolesArgentina >= 3)
            {
                state.Estado = "ganado";
                SaveProgress("Di Carlo");
                HttpContext.Session.SetString(WinKey, "true");
                SetDiCarloGameState(state);
                TempData["mensaje"] = $"¡CANTILO GANA 3-{state.GolesRival}! Derrotaste a Los Robots de Di Carlo.";
                TempData["correcto"] = true;
                return RedirectToAction(nameof(Ganaste));
            }
            // Si están empatados, van a penales
            else if (state.GolesArgentina == state.GolesRival)
            {
                state.EnPenales = true;
                state.Paso -= 1; // Para que muestre la última escena
                SetDiCarloGameState(state);
                TempData["mensaje"] = $"¡Cantilo y Los Robots empataron {state.GolesArgentina}-{state.GolesRival}! Vamos a PENALES. Un tiro decide el partido.";
                TempData["correcto"] = false;
                return RedirectToAction(nameof(DiCarlo));
            }
            // Si la rival tiene más, perdió
            else
            {
                state.Estado = "perdido";
                SetDiCarloGameState(state);
                LimpiarSesion();
                TempData["mensajeError"] = $"Cantilo perdió {state.GolesRival}-{state.GolesArgentina} contra Los Robots de Di Carlo. La sala se cerró.";
                return RedirectToAction(nameof(Perdiste));
            }
        }

        SetDiCarloGameState(state);
        return RedirectToAction(nameof(DiCarlo));
    }

    private List<JugadorDiCarlo> GetEquipoDonofrioActual()
    {
        var equipo = GetDonofrioPlantilla();
        if (equipo.Count == 0)
        {
            return new List<JugadorDiCarlo>();
        }

        var jugadores = GetDonofrioJugadores();
        return jugadores
            .Where(j => equipo.Contains(NormalizePlayerName(j.Nombre), StringComparer.OrdinalIgnoreCase))
            .OrderBy(j => j.Id)
            .ToList();
    }

    private DiCarloGameState GetDiCarloGameState()
    {
        var raw = HttpContext.Session.GetString("dicarloPartido");
        if (string.IsNullOrWhiteSpace(raw))
        {
            return new DiCarloGameState();
        }

        try
        {
            return System.Text.Json.JsonSerializer.Deserialize<DiCarloGameState>(raw) ?? new DiCarloGameState();
        }
        catch
        {
            return new DiCarloGameState();
        }
    }

    private void SetDiCarloGameState(DiCarloGameState state)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(state);
        HttpContext.Session.SetString("dicarloPartido", json);
    }

    private static List<DiCarloEscena> GetDiCarloEscenas()
    {
        return new List<DiCarloEscena>
        {
            new()
            {
                Titulo = "Apertura: Falcao vs Los Robots",
                Texto = "Falcao hace una diagonal perfecta y deja a Julián Álvarez mano a mano contra la defensa robot. ¿Por dónde rematas?",
                Opciones = new[] { "Patear a la izquierda", "Patear al centro", "Patear a la derecha" },
                Correcta = "Patear a la derecha",
                Exito = "¡GOOOOL! Julián mete en escuadra. 1-0 Cantilo vs Los Robots de Di Carlo.",
                Fracaso = "¡No! El portero robot vuela y desvía. Los Robots sacan la pelota de contragolpe.",
                EsAtaque = true,
                Minuto = "5'",
                ResultadoExitoTipo = "GOL",
                ResultadoFracasoTipo = "ATAJADA"
            },
            new()
            {
                Titulo = "Ataque robot: Centro al área",
                Texto = "Los Robots de Di Carlo centran peligrosamente. Estás defendiendo a Cantilo y tenés que despejar o interceptar.",
                Opciones = new[] { "Despejar al fondo del campo", "Cortar el pase", "Hacer falta" },
                Correcta = "Cortar el pase",
                Exito = "¡Excelente defensa! Recuperás la pelota limpio para Cantilo.",
                Fracaso = "¡No! Un delantero robot conecta. El balón entra en la red. 1-1.",
                EsAtaque = false,
                Minuto = "23'",
                ResultadoExitoTipo = "DEFENSOR",
                ResultadoFracasoTipo = "GOL_RIVAL"
            },
            new()
            {
                Titulo = "Contra de Cantilo a toda velocidad",
                Texto = "Cantilo tiene espacio. La cancha está abierta. Enzo Fernández te pasa al hueco.",
                Opciones = new[] { "Pase al costado", "Conducir y avanzar", "Tiro de larga distancia" },
                Correcta = "Conducir y avanzar",
                Exito = "¡Encarás a los robots! Sorteás dos defensores mecánicos y crosás. ¡GOOOOL de Cantilo! 2-1.",
                Fracaso = "¡Probás desde lejos pero el remate se va muy desviado! La mandaste afuera.",
                EsAtaque = true,
                Minuto = "41'",
                ResultadoExitoTipo = "GOL",
                ResultadoFracasoTipo = "AFUERA"
            },
            new()
            {
                Titulo = "Peligro en el área chica",
                Texto = "Un tiro de esquina de Los Robots. La pelota viene al primer palo. ¿Qué haces?",
                Opciones = new[] { "Salir a despejar", "Dejarlo pasar", "Entrecortado en las manos" },
                Correcta = "Salir a despejar",
                Exito = "¡Bien! Despejás antes de que el robot conecte. Esquivaste el peligro.",
                Fracaso = "¡GOOOOL ROBOTS! Un cabezazo mecánico perfecto. 2-2.",
                EsAtaque = false,
                Minuto = "58'",
                ResultadoExitoTipo = "DEFENSOR",
                ResultadoFracasoTipo = "GOL_RIVAL"
            },
            new()
            {
                Titulo = "Presión ofensiva: Minuto 85",
                Texto = "Los Robots tienen que defender. Cantilo genera una oportunidad clarísima. Falcao define solo.",
                Opciones = new[] { "Tiro suave al ángulo", "Remate potente al medio", "Dribbling al arquero" },
                Correcta = "Tiro suave al ángulo",
                Exito = "¡Tiro perfecto! El arquero robot vuela pero no llega. ¡GOOOOL DE CANTILO! 3-2.",
                Fracaso = "¡Casi! El remate impacta en el palo y sale. Último intento fallido.",
                EsAtaque = true,
                Minuto = "85'",
                ResultadoExitoTipo = "GOL",
                ResultadoFracasoTipo = "PALO"
            },
            new()
            {
                Titulo = "Últimos segundos: Defensa total",
                Texto = "Quedan segundos. Los Robots atacan desesperados. Cantilo defiende con todo. Todo o nada.",
                Opciones = new[] { "Marcar apretado", "Permitir el balón y defender", "Agredir temprano" },
                Correcta = "Marcar apretado",
                Exito = "¡TFIIIIIIN! Cantilo gana 3-2 contra Los Robots de Di Carlo. ¡Escapás de la sala!",
                Fracaso = "¡NO! Los Robots meten otra. 3-3. Vamos a penales vs Los Robots.",
                EsAtaque = false,
                Minuto = "90+3'",
                ResultadoExitoTipo = "DEFENSOR",
                ResultadoFracasoTipo = "GOL_RIVAL"
            }
        };
    }

    private static DiCarloEscena GetDiCarloEscena(int paso)
    {
        var escenas = GetDiCarloEscenas();
        var index = Math.Clamp(paso, 0, escenas.Count - 1);
        return escenas[index];
    }

    private static string NormalizeDecision(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized = value.Trim();
        normalized = new string(normalized.Normalize(System.Text.NormalizationForm.FormD)
            .Where(ch => CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark
                && !char.IsPunctuation(ch)
                && !char.IsSymbol(ch)
                && !char.IsControl(ch))
            .ToArray());

        return normalized.Replace(" ", string.Empty, StringComparison.Ordinal).ToUpperInvariant();
    }

    private static string GetChallengeNameBySalaId(int salaId)
    {
        return salaId switch
        {
            1 => "Coudet",
            2 => "Acuna",
            3 => "Demichelis",
            4 => "Tapia",
            5 => "Scaloni",
            6 => "Donofrio",
            7 => "Di Carlo",
            _ => "Coudet"
        };
    }

    public sealed class DiCarloEscena
    {
        public string Titulo { get; set; } = string.Empty;
        public string Texto { get; set; } = string.Empty;
        public string[] Opciones { get; set; } = Array.Empty<string>();
        public string Correcta { get; set; } = string.Empty;
        public string Exito { get; set; } = string.Empty;
        public string Fracaso { get; set; } = string.Empty;
        public bool EsAtaque { get; set; } = true;
        public string Minuto { get; set; } = string.Empty;
        public string ResultadoExitoTipo { get; set; } = string.Empty;
        public string ResultadoFracasoTipo { get; set; } = string.Empty;
    }

    private sealed class DiCarloGameState
    {
        public int Paso { get; set; } = 0;
        public string Estado { get; set; } = "jugando";
        public string? UltimaDecision { get; set; }
        public string? Mensaje { get; set; }
        public int GolesArgentina { get; set; } = 0;
        public int GolesRival { get; set; } = 0;
        public bool EnPenales { get; set; } = false;
        public int? ResultadoPenales { get; set; } = null;
        public string? UltimoMinuto { get; set; }
        public string? UltimoResultadoTipo { get; set; }
        public string? UltimoResultadoTexto { get; set; }
        public bool? UltimoResultadoCorrecto { get; set; }
    }

    private static List<DiCarloSlot> BuildDiCarloBoard(List<JugadorDiCarlo> jugadoresAdivinados)
    {
        var slots = new[]
        {
            new DiCarloSlot { Label = "ST", Categoria = "Delantero" },
            new DiCarloSlot { Label = "ST", Categoria = "Delantero" },
            new DiCarloSlot { Label = "LM", Categoria = "Delantero" },
            new DiCarloSlot { Label = "RM", Categoria = "Delantero" },
            new DiCarloSlot { Label = "CM", Categoria = "Mediocampista" },
            new DiCarloSlot { Label = "CDM", Categoria = "Mediocampista" },
            new DiCarloSlot { Label = "LB", Categoria = "Defensor" },
            new DiCarloSlot { Label = "RB", Categoria = "Defensor" },
            new DiCarloSlot { Label = "CB", Categoria = "Defensor" },
            new DiCarloSlot { Label = "CB", Categoria = "Defensor" },
            new DiCarloSlot { Label = "GK", Categoria = "Arquero" }
        };

        var usados = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var slot in slots)
        {
            slot.Jugador = jugadoresAdivinados
                .FirstOrDefault(j =>
                    string.Equals(j.Posicion, slot.Categoria, StringComparison.OrdinalIgnoreCase)
                    && !usados.Contains(NormalizePlayerName(j.Nombre)));

            if (slot.Jugador != null)
            {
                usados.Add(NormalizePlayerName(slot.Jugador.Nombre));
            }
        }

        return slots.ToList();
    }

    private static string NormalizePlayerName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized = value.Trim();
        normalized = new string(normalized.Normalize(NormalizationForm.FormD)
            .Where(ch => CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark
                && !char.IsPunctuation(ch)
                && !char.IsSymbol(ch)
                && !char.IsControl(ch))
            .ToArray());

        return normalized.Replace(" ", string.Empty, StringComparison.Ordinal).ToUpperInvariant();
    }

    private sealed class DiCarloSlot
    {
        public string Label { get; set; } = string.Empty;
        public string Categoria { get; set; } = string.Empty;
        public JugadorDiCarlo? Jugador { get; set; }
    }

    public IActionResult Ganaste()
    {
        if (!IsWon())
        {
            return RedirectToAction(nameof(Index));
        }

        var tiempoRestante = HttpContext.Session.GetInt32("tiempoRestanteSegundos") ?? 1800;
        GuardarTiempoRestante(tiempoRestante);
        ViewBag.Progress = GetProgress();
        ViewBag.TiempoRestanteSegundos = tiempoRestante;
        return View();
    }

    public IActionResult Perdiste()
    {
        var tiempoRestante = HttpContext.Session.GetInt32("tiempoRestanteSegundos") ?? 1800;
        ViewBag.TiempoRestanteSegundos = tiempoRestante;
        return View();
    }

    [HttpPost]
    [HttpPost]
    public IActionResult Penales(string decision)
    {
        if (!CanAccessChallenge("Di Carlo"))
        {
            return RedirectToAction(nameof(Index));
        }

        var state = GetDiCarloGameState();
        if (!state.EnPenales)
        {
            return RedirectToAction(nameof(DiCarlo));
        }

        // Un penal decide el partido
        var random = new Random();
        var arcosCorner = new[] { "arriba-izquierda", "arriba-derecha", "abajo-izquierda", "abajo-derecha", "centro" };
        var tipoTiro = new[] { "arriba", "abajo", "izquierda", "derecha", "centro" };

        var direccionDecision = NormalizeDecision(decision);
        var direccionCorrecta = NormalizeDecision(arcosCorner[random.Next(arcosCorner.Length)]);

        if (string.Equals(direccionDecision, direccionCorrecta, StringComparison.OrdinalIgnoreCase))
        {
            // Ganó en penales Cantilo
            state.Estado = "ganado";
            state.ResultadoPenales = 1;
            SaveProgress("Di Carlo");
            HttpContext.Session.SetString(WinKey, "true");
            SetDiCarloGameState(state);
            TempData["mensaje"] = $"¡CANTILO GANA EN PENALES {state.GolesArgentina + 1}-{state.GolesRival} a Los Robots de Di Carlo! ¡Escapás de la sala!";
            TempData["correcto"] = true;
            return RedirectToAction(nameof(Ganaste));
        }
        else
        {
            // Perdió en penales contra Los Robots
            state.Estado = "perdido";
            state.ResultadoPenales = 0;
            SetDiCarloGameState(state);
            LimpiarSesion();
            TempData["mensajeError"] = $"¡Cantilo perdió en penales {state.GolesArgentina}-{state.GolesRival + 1} contra Los Robots de Di Carlo! La sala se cerró.";
            return RedirectToAction(nameof(Perdiste));
        }
    }

    [HttpPost]
    public IActionResult Perder()
    {
        GuardarTiempoRestante(0);
        LimpiarSesion();
        TempData["mensajeError"] = "Se acabó el tiempo. La partida finalizo.";
        return Json(new { redirect = Url.Action(nameof(Index)) });
    }

    [HttpPost]
    public IActionResult GuardarTiempo(int? tiempoSegundos)
    {
        if (!tiempoSegundos.HasValue)
        {
            var tiempoActual = HttpContext.Session.GetInt32("tiempoRestanteSegundos") ?? 1800;
            GuardarTiempoRestante(Math.Max(0, tiempoActual));
            return Ok();
        }

        var tiempoRestante = Math.Max(0, tiempoSegundos.Value);
        HttpContext.Session.SetInt32("tiempoRestanteSegundos", tiempoRestante);
        GuardarTiempoRestante(tiempoRestante);
        return Ok();
    }

    private List<string> GetProgress()
    {
        var value = HttpContext.Session.GetString(ProgressKey);
        if (string.IsNullOrWhiteSpace(value))
        {
            return new List<string>();
        }

        return value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private void SaveProgress(string challenge)
    {
        try
        {
            if (Request?.HasFormContentType == true && Request.Form.ContainsKey("tiempoRestanteSegundos"))
            {
                var raw = Request.Form["tiempoRestanteSegundos"].ToString();
                if (int.TryParse(raw, out var parsed))
                {
                    ActualizarTiempoDelFormulario(parsed);
                }
            }
        }
        catch
        {
            // No bloquear la ejecución si hay problemas al leer el formulario.
        }

        var progress = GetProgress();
        var exists = progress.Any(item => string.Equals(item, challenge, StringComparison.OrdinalIgnoreCase));

        if (!exists)
        {
            progress.Add(challenge);
            var progressStr = string.Join(',', progress);
            HttpContext.Session.SetString(ProgressKey, progressStr);

            var nombreParticipante = GetParticipanteActual();
            var codigoActual = HttpContext.Session.GetString("codigoActual");
            var tiempoRestante = HttpContext.Session.GetInt32("tiempoRestanteSegundos") ?? 1800;

            // No generar ni persistir un código de sesión si el participante es el valor por defecto.
            var esParticipantePorDefecto = string.Equals(nombreParticipante, "Jugador", StringComparison.OrdinalIgnoreCase);

            if (!esParticipantePorDefecto)
            {
                if (string.IsNullOrWhiteSpace(codigoActual))
                {
                    codigoActual = GenerarCodigoUnico();
                    HttpContext.Session.SetString("codigoActual", codigoActual);
                }

                _bd.GuardarCodigo(codigoActual, nombreParticipante, progressStr, tiempoRestante);
                GuardarTiempoRestante(tiempoRestante);

                TempData["CodigoSesion"] = codigoActual;
                var salaId = GetSalaIdForChallenge(challenge);
                var codigoSala = _bd.ObtenerCodigoSalaPorId(salaId) ?? string.Empty;
                TempData["CodigoSala"] = codigoSala;
                HttpContext.Session.SetString("codigoSalaActual", codigoSala ?? string.Empty);
            }
        }
    }

    private void GuardarTiempoActualSiHayCodigo()
    {
        var codigoActual = HttpContext.Session.GetString("codigoActual");
        if (string.IsNullOrWhiteSpace(codigoActual))
        {
            return;
        }

        var tiempoRestante = HttpContext.Session.GetInt32("tiempoRestanteSegundos") ?? 1800;
        GuardarTiempoRestante(Math.Max(0, tiempoRestante));
    }

    private void GuardarTiempoRestante(int tiempoSegundos)
    {
        var nombreParticipante = GetParticipanteActual();
        var codigoActual = HttpContext.Session.GetString("codigoActual");
        if (!string.IsNullOrWhiteSpace(codigoActual))
        {
            var progress = GetProgress();
            var progressStr = string.Join(',', progress);
            _bd.GuardarCodigo(codigoActual, nombreParticipante, progressStr, tiempoSegundos);
        }
    }

    private void LimpiarSesion()
    {
        HttpContext.Session.Remove("coudetAttempts");
        HttpContext.Session.Remove("coudetDice");
        HttpContext.Session.Remove("coudetHeld");
        HttpContext.Session.Remove("participante");
        HttpContext.Session.Remove(ProgressKey);
        HttpContext.Session.Remove("codigoActual");
        HttpContext.Session.Remove("tiempoRestanteSegundos");
        HttpContext.Session.Remove("donofrioPlantilla");
        HttpContext.Session.Remove("dicarloPartido");
        foreach (var room in ChallengeOrder)
        {
            HttpContext.Session.Remove(GetIntroSessionKey(room));
        }
    }

    private string GenerarCodigoUnico()
    {
        const string caracteres = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        var random = new Random();
        var codigo = new StringBuilder();

        for (int i = 0; i < 3; i++)
        {
            codigo.Append(caracteres[random.Next(caracteres.Length)]);
        }

        return codigo.ToString();
    }

    private bool IsWon()
    {
        return string.Equals(HttpContext.Session.GetString(WinKey), "true", StringComparison.OrdinalIgnoreCase);
    }

    private bool CanAccessChallenge(string challengeName)
    {
        var index = Array.FindIndex(ChallengeOrder, item => string.Equals(item, challengeName, StringComparison.OrdinalIgnoreCase));
        if (index <= 0)
        {
            return true;
        }

        var previousChallenge = ChallengeOrder[index - 1];
        var progress = GetProgress();
        if (progress.Any(item => string.Equals(item, previousChallenge, StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        var nombreParticipante = GetParticipanteActual();
        var previousSalaId = GetSalaIdForChallenge(previousChallenge);
        return _bd.ValidarAccesoSala(nombreParticipante, previousSalaId);
    }

    private int GetSalaIdForChallenge(string challengeName)
    {
        return challengeName switch
        {
            "Coudet" => 1,
            "Acuna" => 2,
            "Demichelis" => 3,
            "Tapia" => 4,
            "Scaloni" => 5,
            "Donofrio" => 6,
            "Di Carlo" => 7,
            _ => 1
        };
    }

    private IActionResult RedirectToNextChallenge()
    {
        var progress = GetProgress();

        foreach (var challenge in ChallengeOrder)
        {
            if (!progress.Any(item => string.Equals(item, challenge, StringComparison.OrdinalIgnoreCase)))
            {
                var next = challenge switch
                {
                    "Coudet" => nameof(Coudet),
                    "Acuna" => nameof(Acuna),
                    "Demichelis" => nameof(Demichelis),
                    "Tapia" => nameof(Tapia),
                    "Scaloni" => nameof(Scaloni),
                    "Donofrio" => nameof(Donofrio),
                    "Di Carlo" => nameof(DiCarlo),
                    _ => nameof(Index)
                };

                if (string.Equals(next, nameof(Coudet), StringComparison.OrdinalIgnoreCase))
                {
                    TempData["ShowIntro"] = IntroVideoPath;
                }
                else if (string.Equals(next, nameof(Acuna), StringComparison.OrdinalIgnoreCase))
                {
                    TempData["ShowIntro"] = AcunaIntroVideoPath;
                }

                GuardarTiempoActualSiHayCodigo();
                return RedirectToAction(next);
            }
        }

        GuardarTiempoActualSiHayCodigo();
        return RedirectToAction(nameof(Ganaste));
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }

    private void PrepareRoomView(string roomName)
    {
        ViewBag.Participante = GetParticipanteActual();
        ViewBag.HabitacionActual = roomName;
        ViewBag.PartidaEnCurso = true;
        ViewBag.Progress = GetProgress();

        var codigoSesion = HttpContext.Session.GetString("codigoActual");
        if (string.IsNullOrWhiteSpace(codigoSesion))
        {
            codigoSesion = GenerarCodigoUnico();
            HttpContext.Session.SetString("codigoActual", codigoSesion);

            var nombreParticipante = GetParticipanteActual();
            var progress = GetProgress();
            var progressStr = string.Join(',', progress);
            var tiempoRestante = HttpContext.Session.GetInt32("tiempoRestanteSegundos") ?? 1800;
            _bd.GuardarCodigo(codigoSesion, nombreParticipante, progressStr, tiempoRestante);
        }

        ViewBag.CodigoSesion = codigoSesion;

        try
        {
            var salaId = GetSalaIdForChallenge(roomName);
            var codigoSala = _bd.ObtenerCodigoSalaPorId(salaId) ?? string.Empty;

            // Si la BD no responde o no tiene el código, usar un mapeo por defecto
            if (string.IsNullOrWhiteSpace(codigoSala))
            {
                codigoSala = salaId switch
                {
                    1 => "Chacho",
                    2 => "Huevo",
                    3 => "Micho",
                    4 => "Chiqui",
                    5 => "Qatar",
                    6 => "Muñeco",
                    7 => "Mafia",
                    _ => string.Empty
                };
            }

            HttpContext.Session.SetString("codigoSalaActual", codigoSala);
            ViewBag.CodigoSala = codigoSala;
        }
        catch
        {
            ViewBag.CodigoSala = string.Empty;
        }
    }

    private string GetParticipanteActual()
    {
        var nombre = HttpContext.Session.GetString("participante");
        if (string.IsNullOrWhiteSpace(nombre))
        {
            nombre = "Jugador";
            HttpContext.Session.SetString("participante", nombre);
        }

        return nombre;
    }

    private void ActualizarTiempoDelFormulario(int? tiempoRestanteSegundos)
    {
        if (!tiempoRestanteSegundos.HasValue)
        {
            return;
        }

        var tiempoRestante = Math.Max(0, tiempoRestanteSegundos.Value);
        HttpContext.Session.SetInt32("tiempoRestanteSegundos", tiempoRestante);
    }

    private static string GetIntroSessionKey(string roomName)
    {
        return $"introShown_{roomName}";
    }

    private static string? GetIntroVideoForRoom(string roomName)
    {
        return roomName switch
        {
            "Coudet" => IntroVideoPath,
            "Acuna" => AcunaIntroVideoPath,
            _ => null
        };
    }

    private void ConfigureRoomIntro(string roomName)
    {
        var videoPath = GetIntroVideoForRoom(roomName);
        if (string.IsNullOrWhiteSpace(videoPath))
        {
            ViewBag.IntroVideo = null;
            return;
        }

        var sessionKey = GetIntroSessionKey(roomName);
        if (string.Equals(HttpContext.Session.GetString(sessionKey), "true", StringComparison.OrdinalIgnoreCase))
        {
            ViewBag.IntroVideo = null;
            return;
        }

        HttpContext.Session.SetString(sessionKey, "true");
        ViewBag.IntroVideo = videoPath;
    }

    private static int? ObtenerSalaIdPorCodigoFallback(string codigoSala)
    {
        if (string.IsNullOrWhiteSpace(codigoSala))
        {
            return null;
        }

        var codigo = codigoSala.Trim();
        var mapa = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["Chacho"] = 1,
            ["Huevo"] = 2,
            ["Micho"] = 3,
            ["Chiqui"] = 4,
            ["Qatar"] = 5,
            ["Muñeco"] = 6,
            ["Mafia"] = 7
        };

        return mapa.TryGetValue(codigo, out var salaId) ? salaId : null;
    }

    [HttpPost]
    public IActionResult GuardarNombreParticipante(string nombreParticipante)
    {
        var nombre = (nombreParticipante ?? string.Empty).Trim();

        if (string.IsNullOrWhiteSpace(nombre))
        {
            TempData["mensajeError"] = "Ingresá tu nombre para comenzar la partida.";
            return RedirectToAction(nameof(Index));
        }

        HttpContext.Session.SetString("participante", nombre);
        _bd.GuardarParticipante(nombre);

        return RedirectToAction(nameof(Coudet));
    }
}
