# Police des exports

Vera.ttf est une copie non modifiée de Bitstream Vera Sans fournie avec les dépendances ReportLab du runtime de développement. Sa licence et son copyright sont conservés dans bitstream-vera-license.txt, copié avec les sorties du projet. La police est embarquée comme ressource .NET pour ne pas dépendre des polices installées sur l'hôte.

Le PDF embarque la police utilisée. Le DOCX contient une copie obfusquée selon le mécanisme OOXML, avec fontTable et relation interne. Les glyphes PDF absents de cette police sont refusés explicitement, sans remplacement silencieux. Le DOCX conserve l'Unicode source ; son lecteur peut recourir à une police de remplacement pour d'autres alphabets.
