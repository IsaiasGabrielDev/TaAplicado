# Mapa do Nerdin (www.nerdin.com.br), outubro de 2026

Implementado em `Nerdin.cs` (origem "Nerdin" na tela Candidatar; ids `nerdin-<id>` no histórico). Lido no Chrome **sem login** e sem enviar candidatura. Seletores mudam: confira antes de automatizar.

## Busca: `https://www.nerdin.com.br/vagas.php`
- HTML renderizado no servidor (dá para ler com `fetch` + parser, sem esperar JS).
- Parâmetros: `busca_vaga` (cargo), `busca_plataforma` (stack), `busca_local` (cidade), `busca` (campo único), `pagina=2,3…` (20 cards por página).
- Atalhos de filtro são páginas próprias e aceitam os mesmos parâmetros: `vagas-home-office.php`, `vagas-hibrido.php`, `vagas-estagio-junior.php`, `vagas-desenvolvedor-sistemas.php`, `vagas-area-dados.php`, `vagas-infraestrutura-ti.php`.
- Total: texto "N vagas disponíveis" no topo.
- Card: `#vagas-container > .vaga-card`, link em `data-href` (`../vaga_emprego/vaga-<slug>-<id>.php`). **O número no fim do slug é o id da vaga.**
- No card: título `h3.vaga-titulo`, resumo `p.vaga-resumo-linha` (CLT/PJ, nível, modelo), `.vaga-salario-destaque`, `.vaga-empresa-nome`, `.vaga-local-linha`, `.vaga-hashtags`. Botão `a.btn-ver-vaga` ("Quero essa Vaga") só abre a página da vaga.
- O card **não** diz se a vaga é VIP ou externa: só dá para saber abrindo a vaga ou o formulário.
- Próxima página: `nav.paginacao-vagas` (link "Próxima").

## Página da vaga: `/vaga_emprego/vaga-<slug>-<id>.php`
- Título `h2.vaga-pagina-titulo`. Seções "Sobre a Vaga", "Requisitos", "Benefícios" (cabeçalhos `h2.visually-hidden`).
- Três tipos de vaga:
  | Tipo | Como reconhecer | O que fazer |
  |---|---|---|
  | Interna | `a.btn-candidatar-vaga` "Enviar candidatura" → `vaga_candidatura.php?id=<id>` | candidatar (pode ter também "Site da empresa") |
  | Externa | só `a.btn-candidatura-externa-destaque` "Candidatar no site da empresa" (Gupy, forms.gle…) | pular |
  | VIP | sem botão; "disponível exclusivamente para candidatos Premium"; o formulário redireciona para `premium_necessario.php` | pular (volta a ser normal depois) |
- A vaga de exemplo (99196, Gupy) é externa, mas `vaga_candidatura.php?id=99196` também mostra o formulário. Não confirmado se a empresa recebe; tratar como externa.
- Na busca `desenvolvedor .net`: 19 de 20 eram internas (2 com "Site da empresa" também), 1 VIP.

## Formulário: `https://www.nerdin.com.br/vaga_candidatura.php?id=<id>`
**Não precisa de login nem de conta.** Mesma página, sem aba nova, sem captcha, sem honeypot, sem checkbox de termos.
`form#form-candidatura`, `POST multipart/form-data` para a própria URL, com `csrf_token` oculto. Envio é submit comum (sem AJAX).
Depois do envio a mesma URL volta com `.alert.alert-success` "Candidatura enviada com sucesso!" (confirmado na 1ª candidatura real, vaga 99908).
Num navegador novo, o aviso "Sua segurança é importante" (`#nerdin-seguranca-modal`) cobre o formulário até clicar em "Entendi" (`[data-nerdin-seg-ack]`).
Na página da vaga, o botão externo também tem `.btn-candidatar-vaga`: o que distingue a candidatura interna é o link para `vaga_candidatura.php`.
Pretensão CLT tem máscara de dinheiro que lê os dígitos como centavos ("8000" vira "80,00"): mandar com ",00".

Três etapas na mesma página (`div.form-etapa#etapa-1..3`, as ocultas têm `d-none`). Botões `#btn-proximo` / `#btn-anterior`; na última etapa aparece `#btn-enviar-candidatura` ("Enviar Candidatura").

| Etapa | Campos (`name`) |
|---|---|
| 1. Dados básicos | `nome_candidato`, `telefone_candidato`, `email_candidato`, `cidade_candidato` ("Cidade-UF") |
| 2. Profissional | `funcao_candidato` (texto, ex.: "Back End"), `nivel_ingles` (1 Nenhum…5 Fluent), `disponibilidade_inicio_dias` (número), `possui_empresa_aberta` (0/1, opcional), **`palavra_chave_<n>`** (ver abaixo) |
| 3. Finalizar | `pretensao_salarial` (CLT mensal), `pretensao_salarial_pj` (PJ por hora), `curriculo` (arquivo `.pdf,.docx`, **opcional**) |

Todos com `*` têm `required`, menos `possui_empresa_aberta` e `curriculo`.

### Perguntas da vaga (`palavra_chave_<n>`)
- 0 a 3+ por vaga; `<select>` com `<label for>` igual ao nome da tecnologia ("Full Stack", "Angular", ".Net").
- Opções fixas: 1 Nenhum, 2 Acadêmico, 3 Até 6 meses, 4 6m–1a, 5 1–2a, 6 2–3a, 7 3–4a, 8 4–5a, 9 Acima de 5 anos, 10 Possuo Certificação.
- Não são obrigatórias (o padrão é "Nenhum"), mas responder errado prejudica a triagem: o Claude só escolhe o tempo de experiência por tecnologia a partir do perfil. Não há teste com resposta certa.

## Diferenças para o LinkedIn/Indeed que pesam no código
- Sem login: os dados do perfil (nome, e-mail, telefone, cidade, pretensão) vêm todos do app; nada vem preenchido.
- Formulário fixo: dá para preencher quase tudo sem o Claude; ele só entra para `funcao_candidato` e `palavra_chave_*`.
- Modo Teste: parar na etapa 3 sem clicar em `#btn-enviar-candidatura`.
- Depois da última página, `pagina=N` repete a última: parar quando nada for novo.
- Sem login não há histórico no site: o app guarda os ids (`nerdin-<id>`) para não repetir.
