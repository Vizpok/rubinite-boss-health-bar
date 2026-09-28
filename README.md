# Rubinite: barra de vida de los jefes siempre visible

### ⬇️ [Descargar BossHealthBar-v1.0.zip](https://github.com/Vizpok/rubinite-boss-health-bar/raw/main/BossHealthBar-v1.0.zip)

Cuando terminas la primera vuelta de [Rubinite](https://store.steampowered.com/app/1845250/Rubinite/), el juego deja de mostrar la barra de vida de los jefes. Pasa en la segunda vuelta de la historia y también al repetir en la Zona de Desafío las peleas en su versión "real". Este mod hace que la barra se vea siempre, como en la primera vuelta. Lo demás de las peleas no cambia.

![Barra de vida de La Bestia en la parte superior de la pantalla](capturas/barra-jefe.webp)

## Cómo instalar

1. Descarga el .zip con el enlace de arriba y descomprímelo.
2. Cierra el juego, abre `BossHealthBar.exe` y elige la opción **1**. Deja los archivos que vienen en el .zip juntos en la misma carpeta.

El programa encuentra el juego solo. Si no lo detecta, arrastra la carpeta del juego encima del .exe.

Windows puede mostrar el aviso de "Windows protegió tu PC" porque el programa no está firmado; dale a *Más información* y luego a *Ejecutar de todas formas*.

**Para quitarlo:** abre el programa otra vez y elige la opción **2**, o verifica los archivos del juego desde Steam.

**Si el juego se actualiza** y la barra vuelve a desaparecer, solo abre el programa de nuevo.

## Qué cambia

Solo toca una parte muy pequeña de `Rubinite_Data/Managed/Assembly-CSharp.dll`: la instrucción que decide ocultar la barra (`BossUI.Start`). Se puede usar junto con la [traducción al español](https://github.com/Vizpok/rubinite-spanish-translation) y otros mods sin que se pisen.

## Compilar

El código está en `codigo/`. Con el compilador de C# que ya trae Windows (.NET Framework 4) y `Mono.Cecil.dll` en la misma carpeta, ejecuta `compilar.bat`.

El programa incluye [Mono.Cecil](https://github.com/jbevain/cecil) (licencia MIT, ver `codigo/LICENCIAS-TERCEROS.txt`). Solo se usa para leer dónde está el método dentro de la DLL.

## Complemento: vista previa del daño

Si quieres ir un paso más allá, está el mod [Vista previa del daño](https://github.com/Vizpok/rubinite-damage-preview). Marca en amarillo, dentro de la barra del jefe, cuánta vida le quitarías con la Estocada según las marcas que llevas acumuladas, y el tramo desaparece cuando te golpean y pierdes las marcas.

Son mods separados. Puedes instalar solo uno o los dos, pero juntos tienen más sentido: con este la barra se ve siempre, y con el otro sabes cuánto le vas a quitar.

---

Mod hecho por un fan, sin relación con los desarrolladores. Aquí no se incluye ningún archivo del juego; necesitas tenerlo comprado para usarlo.
