<?php
    $mysql = new mysqli('localhost', 'junemay00', 'password', 'junemay00');

    $user_srl = intval($_POST['user_srl']);
    $playtime = intval($_POST['playtime']);
    $help = intval($_POST['help']);
    $medkit = intval($_POST['medkit']);
    $headshot = intval($_POST['headshot']);

    $score = ($help * 100) + ($medkit * 100) + ($headshot * 100);
    $score -= min(($playtime * 2), 600);

    $mysql->query("INSERT INTO 1917_pandemic_ranking (user_srl, playtime, help, medkit, headshot, score) VALUES ('{$user_srl}', '{$playtime}', '{$help}', '{$medkit}', '{$headshot}', '{$score}');");
?>
