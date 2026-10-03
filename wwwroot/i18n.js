// English for the whole window. Portuguese is the source language: every key is the text exactly as the app (or the
// host) writes it, so app.js and the C# side keep their logic, logs and saved history in one language, and only what is
// on screen changes. A MutationObserver translates text nodes and a few attributes as the page renders; app.js calls
// t() only where it glues sentences together. Anything inside translate="no" (answers, job titles, names) is left alone.
'use strict';

let LANG = 'pt';
const pl = (n, one, many) => `${n} ${+n === 1 ? one : many}`;

const EN = {
  // top bar and tabs
  'Telas': 'Screens', 'Candidatar': 'Apply', 'Avisos': 'Questions', 'Histórico': 'History', 'Respostas salvas': 'Saved answers',
  'Perfil': 'Profile', 'Perguntar': 'Ask', 'Demonstração · dados fictícios': 'Demo · fake data', 'Usuário': 'User', 'Idioma': 'Language',
  'Quem está usando o app?': 'Who is using the app?', 'Nome de quem vai usar': 'Name of who will use it', 'Novo usuário': 'New user',
  'Pare a execução para trocar de usuário.': 'Stop the run to switch users.', 'Trocar de usuário': 'Switch user',

  // Candidatar
  'Antes de começar': 'Before you start', 'Site das vagas': 'Job site', 'Origem das vagas': 'Job source', 'Recomendadas': 'Recommended',
  'Baseado nas preferências': 'Based on your preferences', 'Combinam com seu perfil': 'Match your profile', 'Pesquisa': 'Search',
  'Termos da pesquisa': 'Search terms', 'ex.: desenvolvedor .NET remoto': 'e.g. remote .NET developer', 'Máximo': 'Max',
  'Tipo de execução': 'Run type', 'De verdade': 'For real', 'Teste': 'Test', 'Seleção das vagas': 'Job selection',
  'Ultra seleção': 'Ultra selection', 'Nota mínima': 'Minimum score', 'Pesquisas para o seu perfil': 'Searches for your profile',
  'Claude pensando…': 'Claude thinking…', 'Sugerir outras': 'Suggest others', 'Pedir sugestões ao Claude': 'Ask Claude for suggestions',
  'Escreva os termos da pesquisa': 'Type the search terms',
  'avisos esperando você': 'questions waiting for you', 'aviso esperando você': 'question waiting for you',
  'Desde o início': 'All time', 'Em andamento': 'In progress', 'Atividade': 'Activity',
  'Parando…': 'Stopping…', 'Parar': 'Stop', 'Confirmar envio': 'Confirm send', 'Candidatar em teste': 'Apply in test mode',
  'Candidatar de verdade': 'Apply for real', 'Só a fila': 'Only the queue', 'Pesquisa “{q}”': 'Search “{q}”',
  'Ultra seleção, nota ≥ {n}': 'Ultra selection, score ≥ {n}', 'Normal': 'Normal', 'até {n}': 'up to {n}', 'você': 'you',
  '{ticket}, em nome de {me}. Clique de novo para confirmar; mude algo acima para cancelar.':
    '{ticket}, in the name of {me}. Click again to confirm; change anything above to cancel.',
  'Analisa até {a}, {verb} até {max} com nota ≥ {min}.': 'Checks up to {a}, {verb} up to {max} scoring ≥ {min}.',
  'testa': 'tests', 'envia': 'sends', 'Preenche e descarta. Nada é enviado.': 'Fills in and discards. Nothing is sent.',
  'Até {n} em seu nome.': 'Up to {n} in your name.',
  'Muitos envios num dia podem fazer o LinkedIn restringir a conta.': 'Many sends in one day can make LinkedIn restrict the account.',
  'nada é enviado': 'nothing is sent', 'Enviando de verdade': 'Sending for real', 'em seu nome': 'in your name',
  'Parar análise e aplicar já': 'Stop checking and apply now', 'Parando a análise…': 'Stopping the check…',
  'nenhuma vaga processada ainda': 'no job processed yet', 'Próxima vaga em': 'Next job in', 'Numa vaga agora': 'On a job now',
  'Parada por você': 'Stopped by you', 'Parou:': 'Stopped:', 'Última execução': 'Last run', 'Última execução (teste)': 'Last run (test)',
  'Execução terminada': 'Run finished', 'Filtros': 'Filters', 'Nesta execução': 'This run', 'Na última execução': 'Last run',
  'Editar': 'Edit', 'nenhuma empresa ou palavra': 'no company or word', ' e mais {n}': ' and {n} more', 'só': 'only', 'prioriza': 'prefers',
  'sem IA': 'no AI', 'Claude dá nota': 'Claude scores', 'Claude julga': 'Claude judges', 'sem Claude': 'no Claude',
  'filtros pularam {n}': 'filters skipped {n}', 'nenhuma vaga pulada pelos filtros': 'no job skipped by filters',
  'Entre no LinkedIn na janela do Chrome. A execução continua sozinha depois do login.': 'Log in to LinkedIn in the Chrome window. The run resumes by itself after login.',
  'Entre no Indeed na janela do Chrome. A execução continua sozinha depois do login.': 'Log in to Indeed in the Chrome window. The run resumes by itself after login.',
  'O Indeed não mostrou a lista de vagas. Se houver uma verificação, resolva na janela do Chrome; a execução continua sozinha.':
    'Indeed did not show the job list. If there is a check, solve it in the Chrome window; the run resumes by itself.',
  'O Indeed pediu uma verificação: resolva na janela do Chrome. A execução continua sozinha depois.': 'Indeed asked for a check: solve it in the Chrome window. The run resumes by itself afterwards.',
  '{n} no perfil; cada uma pode virar aviso.': '{n} in your profile; each one can become a question for you.', 'Abrir perfil': 'Open profile',

  // setup checklist
  'Sem IA': 'No AI', 'Modo sem IA: o Claude não é usado.': 'No-AI mode: Claude is not used.', 'Verificando': 'Checking',
  'Testando o Claude pela bridge…': 'Testing Claude through the bridge…', 'Ok': 'Ok', 'O Claude está respondendo.': 'Claude is answering.',
  'Sem resposta': 'No answer', 'Claude Code: a IA que lê as vagas e responde os formulários': 'Claude Code: the AI that reads the jobs and answers the forms',
  'Precisa de uma conta Claude': 'It needs a Claude account', 'paga, Pro ou Max': 'paid, Pro or Max',
  '(assine em claude.ai). A conta grátis não dá acesso ao Claude Code. O uso sai do limite do seu plano; o app não cobra nada.':
    '(subscribe at claude.ai). The free account has no access to Claude Code. Usage comes out of your plan’s limit; the app charges nothing.',
  'Instalar o Claude Code.': 'Install Claude Code.', 'Abre uma janela do PowerShell que roda o instalador oficial da Anthropic (':
    'Opens a PowerShell window that runs Anthropic’s official installer (', ') e, se faltar, o Git.': ') and, if missing, Git.',
  'Entrar na conta.': 'Sign in.', 'Abre o Claude numa janela; faça login no navegador com a conta Pro ou Max e, na caixa de conversa, digite':
    'Opens Claude in a window; log in in the browser with the Pro or Max account and, in the chat box, type',
  'Ao fechar cada janela, o app testa sozinho. Se já estava tudo instalado e logado e ainda falha, pode ser o limite de uso do plano.':
    'When each window closes, the app tests by itself. If everything was installed and logged in and it still fails, it may be the plan’s usage limit.',
  'Sem conta paga? Siga': 'No paid account? Go', ': as perguntas novas viram avisos para você responder uma vez, e as respostas salvas preenchem o resto.':
    ': new questions become questions for you to answer once, and saved answers fill in the rest.',
  '1. Instalar o Claude Code': '1. Install Claude Code', '2. Entrar na conta': '2. Sign in', 'Testar de novo': 'Test again', 'Seguir sem IA': 'Go without AI',
  'Abrindo o Google Chrome em segundo plano para testar…': 'Opening Google Chrome in the background to test…',
  'O Google Chrome abre e responde ao app.': 'Google Chrome opens and answers the app.', 'Faltando': 'Missing',
  'O app não conseguiu abrir o Google Chrome, e é nele que as candidaturas são feitas.': 'The app could not open Google Chrome, and that is where applications happen.',
  'Instalar o Chrome': 'Install Chrome', 'abre uma janela do PowerShell que instala pelo winget (': 'opens a PowerShell window that installs it with winget (',
  'Lido': 'Read', 'Você sabe para onde seus dados vão.': 'You know where your data goes.', 'Pendente': 'Pending',
  'Seus dados: o que fica aqui e o que é enviado': 'Your data: what stays here and what is sent', 'Li e entendi': 'I read and understood',
  'Aceito': 'Accepted', 'Risco da automação entendido.': 'Automation risk understood.',
  'Automatizar candidaturas vai contra os termos de uso do LinkedIn e a conta pode ser restringida. O app usa intervalos humanos e para no limite diário, mas o risco é seu.':
    'Automating applications goes against LinkedIn’s terms of use and the account can be restricted. The app uses human-like pauses and stops at the daily limit, but the risk is yours.',
  'Entendi e aceito': 'I understand and accept',
  'Seus dados ficam só neste computador; o desenvolvedor do app não recebe nada. Para funcionar, o app envia:':
    'Your data stays only on this computer; the app’s developer receives nothing. To work, the app sends:',
  'à Anthropic (Claude)': 'to Anthropic (Claude)',
  ', pela conta Claude logada neste computador: o seu perfil, as perguntas dos formulários, as descrições das vagas e textos que você importar;':
    ', through the Claude account logged in on this computer: your profile, the form questions, the job descriptions and texts you import;',
  'ao LinkedIn e ao Indeed': 'to LinkedIn and Indeed', ': as respostas das candidaturas.': ': the application answers.',
  'Numa conta pessoal da Anthropic, o uso das conversas para treinar modelos depende da configuração de privacidade dessa conta. Usuários do app separam os dados por organização, não por segurança: quem usa este usuário do Windows consegue abrir as pastas de todos. Para separar de verdade, use um usuário do Windows por pessoa.':
    'On a personal Anthropic account, whether conversations train models depends on that account’s privacy settings. App users split data for organization, not security: whoever uses this Windows user can open everyone’s folders. To truly separate, use one Windows user per person.',

  // avisos
  'Perguntas que o Claude não soube responder pelo seu perfil. Ao responder, a resposta vai para Respostas salvas e a vaga volta para a fila da próxima execução.':
    'Questions Claude could not answer from your profile. Your answer goes to Saved answers and the job goes back to the queue for the next run.',
  'Pedir ao Claude de novo': 'Ask Claude again', 'Claude revisando…': 'Claude reviewing…', 'Sugestão do Claude:': 'Claude’s suggestion:',
  '. Ele não teve certeza pelo seu perfil.': '. It was not sure from your profile.', 'O Claude não encontrou isso no seu perfil.': 'Claude did not find this in your profile.',
  'Responder': 'Answer', 'Respondido:': 'Answered:', 'Respondido': 'Answered', 'Desfazer': 'Undo', 'Nenhum aviso.': 'No questions.',
  'Quando o Claude não tiver certeza de uma resposta, a pergunta fica aqui esperando você, e a execução segue com as outras vagas.':
    'When Claude is not sure of an answer, the question waits here for you, and the run goes on with the other jobs.',

  // statuses (stamps, filters, counters)
  'Enviada': 'Sent', 'Aguardando você': 'Waiting for you', 'Na fila': 'Queued', 'Pulada': 'Skipped', 'Falhou': 'Failed',
  'Enviadas': 'Sent', 'Puladas': 'Skipped', 'Falhas': 'Failed', 'Todas': 'All',
  'sem Easy Apply': 'no Easy Apply', 'a janela do Easy Apply não abriu': 'the Easy Apply window did not open',
  'botão Avançar não encontrado': 'Next button not found', 'travou numa etapa': 'stuck on a step', 'etapas demais': 'too many steps',

  // in progress, activity
  'Vaga anterior · aguardando a próxima': 'Previous job · waiting for the next', 'Última vaga': 'Last job', 'Vaga aberta': 'Job opened',
  'Nenhuma vaga em andamento. Escolha a origem das vagas acima e clique em Candidatar.': 'No job in progress. Pick the job source above and click Apply.',
  'A atividade aparece aqui durante a execução.': 'Activity shows up here during the run.',

  // histórico
  'Filtrar por situação': 'Filter by status', 'Buscar vaga ou empresa': 'Search job or company', 'Vaga': 'Job', 'Título': 'Title',
  'Empresa': 'Company', 'Data': 'Date', 'Situação': 'Status', 'Abrir no LinkedIn': 'Open on LinkedIn',
  'Nenhuma vaga com esse termo.': 'No job with that term.', 'Nenhuma vaga ainda.': 'No jobs yet.',
  'Cada vaga processada entra aqui com a data e a situação.': 'Every processed job lands here with its date and status.',
  'Nenhuma vaga com essa situação.': 'No job with that status.', 'O que aconteceu': 'What happened',
  'Respostas enviadas em seu nome': 'Answers sent in your name', 'Preenchida em teste (descartada)': 'Filled in test mode (discarded)',
  'Não enviada: aguardando você': 'Not sent: waiting for you', 'Não enviada: na fila da próxima execução': 'Not sent: queued for the next run',
  'Nada foi enviado': 'Nothing was sent', 'Nada foi enviado.': 'Nothing was sent.', 'Nada foi enviado ainda.': 'Nothing was sent yet.',
  'Nenhum campo preenchido pelo app: o LinkedIn já tinha tudo.': 'No field filled by the app: the site already had everything.',
  'Nenhum campo preenchido pelo app. Nada foi enviado.': 'No field filled by the app. Nothing was sent.',
  'O app não preencheu nenhum campo desta vaga.': 'The app filled no field for this job.',
  'O que o app tinha preenchido antes de descartar:': 'What the app had filled in before discarding:',
  'Sem registro das etapas desta vaga.': 'No record of this job’s steps.', 'Responder os avisos': 'Answer the questions',

  // respostas salvas
  'Limpar filtros': 'Clear filters', 'Aprovar todas': 'Approve all', 'Pergunta': 'Question', 'Resposta': 'Answer', 'Origem': 'Source',
  'Ações': 'Actions', 'Filtrar pergunta': 'Filter question', 'Filtrar pela pergunta': 'Filter by question', 'Filtrar resposta': 'Filter answer',
  'Filtrar pela resposta': 'Filter by answer', 'Filtrar pela origem': 'Filter by source', 'Você': 'You',
  'Claude · a conferir': 'Claude · to check', 'Claude · conferida': 'Claude · checked', '{a} de {b}': '{a} of {b}',
  'Respostas que o Claude deu sozinho, sem te interromper. Se estiver certa, aprove; se não, corrija e ela vale para as próximas vagas.':
    'Answers Claude gave on its own, without interrupting you. If one is right, approve it; if not, fix it and it applies to the next jobs.',
  'Toda resposta daqui é reusada sem perguntar de novo quando outra vaga fizer a mesma pergunta.':
    'Every answer here is reused without asking again when another job asks the same question.',
  'Está certa': 'It’s right', 'Excluir resposta': 'Delete answer', 'Nenhuma resposta com esses filtros.': 'No answer with these filters.',
  'Nenhuma resposta salva ainda.': 'No saved answers yet.',
  'As respostas do Claude e as suas entram aqui e são reusadas sem perguntar de novo.': 'Claude’s answers and yours land here and are reused without asking again.',
  'Resposta salva': 'Answer saved', 'Resposta aprovada': 'Answer approved',

  // perfil
  'Tudo o que o Claude responde sai em seu nome, a partir destas respostas. Pergunta em branco vira aviso quando uma vaga perguntar.':
    'Everything Claude answers goes out in your name, from these answers. A blank question becomes a question for you when a job asks it.',
  'Importar do LinkedIn ou de um texto': 'Import from LinkedIn or from a text', 'link do perfil, currículo, anotações': 'profile link, resume, notes',
  'Link do perfil no LinkedIn': 'LinkedIn profile link', 'linkedin.com/in/seu-nome': 'linkedin.com/in/your-name', 'Ler o perfil': 'Read the profile',
  'O app abre o perfil na janela do Chrome dele (com o seu login do LinkedIn), lê experiência, formação, competências, certificações e idiomas, e manda o texto ao Claude. Se pedir login, entre na janela do Chrome.':
    'The app opens the profile in its Chrome window (with your LinkedIn login), reads experience, education, skills, certifications and languages, and sends the text to Claude. If it asks you to log in, do it in the Chrome window.',
  'Ou cole um texto': 'Or paste a text',
  'Cole o texto e o Claude preenche as perguntas em branco e acrescenta às listas (tecnologias, cargos, experiência) só o que ainda não está lá. Respostas únicas que você já deu não mudam. Nada é salvo antes de você revisar e clicar em Salvar perfil.':
    'Paste the text and Claude fills in the blank questions and adds to the lists (technologies, roles, experience) only what is not there yet. Single answers you already gave do not change. Nothing is saved before you review and click Save profile.',
  'Texto para importar': 'Text to import', 'Cole aqui o seu currículo ou qualquer texto sobre a sua carreira': 'Paste your resume or any text about your career here',
  'Preencher com o Claude': 'Fill with Claude', 'Salvar perfil': 'Save profile', 'Currículos': 'Resumes',
  'Na etapa de currículo, o app vê o idioma da vaga e usa o arquivo desse idioma: seleciona se o LinkedIn já o tem, senão envia. Sem arquivo no idioma, fica o currículo que o LinkedIn já marcou. PDF, DOC ou DOCX de até 2 MB;':
    'At the resume step, the app looks at the job’s language and uses that language’s file: selects it if LinkedIn has it, otherwise uploads it. With no file in that language, the resume LinkedIn already marked stays. PDF, DOC or DOCX up to 2 MB;',
  'salva na hora': 'saved right away', 'Filtros de vagas': 'Job filters',
  'Vagas que o app pula antes de gastar uma chamada do Claude. Diferente do perfil acima,': 'Jobs the app skips before spending a Claude call. Unlike the profile above, this',
  'salva sozinho': 'saves by itself', 'enquanto você digita; vale a partir da próxima execução.': 'as you type; it applies from the next run on.',
  'Empresas ignoradas': 'Ignored companies', 'Uma por linha\nAcme\nConsultoria Exemplo': 'One per line\nAcme\nExample Consulting',
  'Uma por linha. Pula toda empresa cujo nome contenha o texto, então prefira o nome inteiro.': 'One per line. Skips every company whose name contains the text, so prefer the full name.',
  'Palavras que fazem pular a vaga': 'Words that skip the job', 'Uma por linha\nestágio\nPHP\npresencial': 'One per line\ninternship\nPHP\non-site',
  'Uma por linha ou separadas por vírgula. Palavra inteira, no título ou na descrição; maiúsculas e acentos não importam.':
    'One per line or comma-separated. Whole word, in the title or description; case and accents do not matter.',
  'País das vagas': 'Job country', 'Brasil': 'Brazil', 'Regra para o país': 'Country rule', 'Tanto faz': 'Any', 'Priorizar': 'Prefer', 'Só esse': 'Only this',
  'Idioma da vaga': 'Job language', 'Português': 'Portuguese', 'Inglês': 'English', 'Regra para o idioma': 'Language rule',
  ': essas vagas vão primeiro; as outras só entram se a lista acabar antes do Máximo (na Ultra, ficam atrás no ranking).':
    ': these jobs go first; the others only come in if the list runs out before Max (in Ultra, they rank lower).',
  ': as outras são puladas. O Claude lê cada vaga para saber o país e o idioma, mesmo com a opção abaixo desligada.':
    ': the others are skipped. Claude reads each job to learn its country and language, even with the option below off.',
  'Claude decide se a vaga combina com o perfil': 'Claude decides whether the job fits the profile',
  'Desligado, só as listas e as regras de país e idioma filtram. A Ultra seleção sempre usa o Claude, porque precisa das notas.':
    'Off, only the lists and the country and language rules filter. Ultra selection always uses Claude, because it needs the scores.',
  'Modo sem IA': 'No-AI mode',
  'Nada é enviado ao Claude. Pergunta nova vira aviso para você responder uma vez; as respostas salvas preenchem o resto. Sem notas, a Ultra seleção fica desligada.':
    'Nothing is sent to Claude. A new question becomes a question for you to answer once; saved answers fill in the rest. Without scores, Ultra selection is off.',
  'Seus dados': 'Your data', 'Exportar perfil (.zip)': 'Export profile (.zip)', 'Importar perfil (.zip)': 'Import profile (.zip)',
  'Apagar este usuário…': 'Delete this user…', 'Apagar para sempre': 'Delete forever', 'Cancelar': 'Cancel',
  'Alterações não salvas': 'Unsaved changes', 'Ir para Candidatar': 'Go to Apply', 'Completo': 'Complete',
  'pergunta em branco. Cada uma vira aviso quando uma vaga perguntar.': 'blank question. Each one becomes a question for you when a job asks it.',
  'perguntas em branco. Cada uma vira aviso quando uma vaga perguntar.': 'blank questions. Each one becomes a question for you when a job asks it.',
  'Todas as perguntas respondidas.': 'All questions answered.', 'em branco': 'blank', 'Não informado': 'Not given',
  'Competência ou ferramenta': 'Skill or tool', 'Anos': 'Years', 'Remover competência': 'Remove skill', 'Adicionar competência': 'Add skill',
  'Nenhum arquivo': 'No file', 'Trocar arquivo…': 'Change file…', 'Escolher arquivo…': 'Choose file…', 'Remover': 'Remove',
  'O Claude está lendo o texto…': 'Claude is reading the text…', 'Use o link de um perfil, por exemplo linkedin.com/in/seu-nome': 'Use a profile link, for example linkedin.com/in/your-name',
  'Pare a execução antes: o Chrome do app está em uso.': 'Stop the run first: the app’s Chrome is in use.',
  'Lendo o perfil no Chrome… Se pedir login, entre na janela do Chrome. Depois o Claude preenche (pode levar 1 a 2 minutos).':
    'Reading the profile in Chrome… If it asks you to log in, do it in the Chrome window. Then Claude fills it in (may take 1 to 2 minutes).',
  '{x}. Revise e clique em Salvar perfil.': '{x}. Review and click Save profile.',
  'Nada novo: o texto não traz nada que ainda falte no perfil.': 'Nothing new: the text has nothing the profile still lacks.',
  'Apaga deste computador tudo de {name}: {list}, o perfil, a atividade e o login do LinkedIn no app. Não dá para desfazer.':
    'Deletes everything of {name} from this computer: {list}, the profile, the activity and the LinkedIn login in the app. This cannot be undone.',
  'Modo sem IA ligado': 'No-AI mode on', 'Modo sem IA desligado': 'No-AI mode off', 'Filtros salvos': 'Filters saved', 'Usuário apagado': 'User deleted',

  // profile questionnaire (labels, hints, options: the markdown Claude reads stays Portuguese)
  'Identificação': 'Identification', 'Nome completo': 'Full name', 'Cidade, estado e país': 'City, state and country',
  'E-mail para candidaturas': 'E-mail for applications', 'Telefone': 'Phone', 'LinkedIn, portfólio ou site': 'LinkedIn, portfolio or website',
  'Carreira': 'Career', 'Cargo atual e empresa': 'Current role and company', 'Cargos anteriores (cargo, empresa, período)': 'Previous roles (role, company, period)',
  'Que vaga você procura?': 'What job are you looking for?', 'Foco da vaga': 'Job focus', 'A mesma função, em qualquer setor': 'The same role, in any sector',
  'Só o setor em que já trabalho': 'Only the sector I already work in',
  'Ex.: recepcionista de clínica veterinária também recebe vagas de recepção em escritório, clínica médica ou advocacia.':
    'E.g. a vet clinic receptionist also gets reception jobs at offices, medical clinics or law firms.',
  'Anos de experiência na função que você procura': 'Years of experience in the role you are looking for',
  'Anos de experiência na área, no total': 'Years of experience in the field, in total',
  'Inclui funções parecidas ou de apoio. Ex.: 3 como analista financeiro, 6 em finanças.': 'Includes similar or support roles. E.g. 3 as financial analyst, 6 in finance.',
  'Experiência com liderança de pessoas': 'People leadership experience', 'Em branco se não tiver.': 'Blank if none.',
  'Competências e ferramentas': 'Skills and tools', 'Anos de experiência por competência ou ferramenta': 'Years of experience per skill or tool',
  'O que as vagas da sua área perguntam: Excel, SAP, AutoCAD, vendas B2B, atendimento, C#… O que ficar fora desta lista vira aviso.':
    'What jobs in your field ask about: Excel, SAP, AutoCAD, B2B sales, customer service, C#… Anything left out of this list becomes a question for you.',
  'Outras competências e ferramentas que já usei (sem tempo definido)': 'Other skills and tools I have used (no set time)',
  'Competências e ferramentas que não domino': 'Skills and tools I don’t know', 'Para estas o Claude responde “não” sem perguntar.': 'For these Claude answers “no” without asking.',
  'Setores e mercados em que já trabalhei': 'Sectors and markets I have worked in', 'Idiomas e formação': 'Languages and education',
  'Nível de inglês': 'English level', 'Básico (A1–A2)': 'Basic (A1–A2)', 'Intermediário (B1)': 'Intermediate (B1)',
  'Intermediário avançado (B2)': 'Upper intermediate (B2)', 'Avançado (C1)': 'Advanced (C1)', 'Fluente (C2)': 'Fluent (C2)',
  'Outros idiomas': 'Other languages', 'Formação (curso, instituição, conclusão)': 'Education (course, institution, completion)',
  'Certificações': 'Certifications', 'Em branco: o Claude nunca cita certificação.': 'Blank: Claude never mentions a certification.',
  'Registro profissional (CRM, OAB, CREA, CRC, COREN…)': 'Professional license (bar, medical, engineering…)',
  'Número e estado. Escreva “não se aplica” se a sua profissão não exige.': 'Number and state. Write “not applicable” if your profession does not require one.',
  'Contrato e logística': 'Contract and logistics', 'Tipo de contrato aceito': 'Accepted contract type', 'CLT ou PJ': 'CLT or PJ',
  'Pretensão salarial mensal CLT (R$ bruto)': 'Monthly salary expectation, employee/CLT (gross)', 'Pretensão PJ': 'Contractor/PJ expectation',
  'Em branco: cada pergunta de PJ vira aviso para você decidir.': 'Blank: every contractor question becomes a question for you to decide.',
  'Observações sobre remuneração': 'Notes on pay', 'Disponibilidade para início / aviso prévio': 'Start availability / notice period',
  'Modelo de trabalho': 'Work model', 'Remoto': 'Remote', 'Híbrido': 'Hybrid', 'Presencial': 'On-site', 'Qualquer um': 'Any',
  'Aceita mudar de cidade?': 'Willing to relocate?', 'Sim': 'Yes', 'Não': 'No', 'Depende da vaga': 'Depends on the job',
  'Cidades onde aceita trabalhar presencial ou híbrido': 'Cities where you accept on-site or hybrid work',
  'Até onde dá para ir todo dia. Ex.: Mauá, Santo André, São Paulo. Vaga presencial em outra cidade é pulada.':
    'As far as you can commute every day. On-site jobs in other cities are skipped.',
  'Aceita viajar a trabalho?': 'Willing to travel for work?', 'Eventualmente': 'Occasionally', 'Autorizado a trabalhar no Brasil?': 'Authorized to work in Brazil?',
  'Autorização em outros países / precisa de patrocínio de visto?': 'Authorization in other countries / need visa sponsorship?',
  'Possui CNH?': 'Driver’s license?', 'Apresentação': 'Introduction', 'Resumo profissional (“fale sobre você”)': 'Professional summary (“tell us about yourself”)',
  'Respostas prontas para perguntas frequentes': 'Ready answers to frequent questions', 'O que nunca deve ser dito': 'What must never be said',

  // perguntar
  'Perguntas': 'Questions', 'Respostas': 'Answers', 'Copiar todas': 'Copy all', 'Mensagem para responder': 'Reply message', 'Copiar mensagem': 'Copy message',
  'Cole aqui as perguntas, do jeito que vieram\n\nQuantos anos de experiência você tem com C#?\nQual a sua pretensão salarial?\nVocê tem disponibilidade para início imediato?':
    'Paste the questions here, as they came\n\nHow many years of experience do you have with C#?\nWhat is your salary expectation?\nCan you start right away?',
  'Modo sem IA: cada linha é uma pergunta, procurada nas respostas salvas. O que não estiver lá, você responde e salva para as próximas vagas.':
    'No-AI mode: each line is a question, looked up in the saved answers. What is not there, you answer and save for the next jobs.',
  'Cole perguntas de um formulário fora do Easy Apply ou da mensagem de um recrutador. O Claude responde pelo seu perfil e pelas respostas salvas; confira, copie e salve o que quiser reusar.':
    'Paste questions from a form outside Easy Apply or from a recruiter’s message. Claude answers from your profile and saved answers; check, copy and save what you want to reuse.',
  'Procurar nas respostas salvas': 'Look up in saved answers', 'Claude respondendo…': 'Claude answering…', 'Responder com o Claude': 'Answer with Claude',
  'O Claude está lendo as perguntas (pode levar até 1 minuto).': 'Claude is reading the questions (may take up to 1 minute).',
  'Não está no seu perfil nem nas respostas salvas. Escreva a resposta.': 'Not in your profile or saved answers. Write the answer.',
  'Sem certeza pelo seu perfil. Confira antes de usar.': 'Not sure from your profile. Check before using.', 'Nas respostas salvas': 'In saved answers',
  'Copiar': 'Copy', 'Copiada': 'Copied', 'Não deu para copiar': 'Could not copy', 'Trocar a resposta salva': 'Replace the saved answer',
  'Salvar nas respostas': 'Save to answers', 'Sua resposta': 'Your answer', '[sua resposta]': '[your answer]', 'Nenhuma pergunta ainda.': 'No questions yet.',
  'As respostas aparecem aqui, uma por pergunta, para você conferir, copiar e salvar.': 'Answers show up here, one per question, for you to check, copy and save.',
  '{n}, {m} para conferir.': '{n}, {m} to check.', '{n}, todas respondidas.': '{n}, all answered.',
  '{n} entre colchetes para você completar antes de enviar.': '{n} in brackets for you to fill in before sending.', '{label} copiada': '{label} copied',

  // words glued by plural()
  'aviso': 'question', 'avisos': 'questions', 'vaga': 'job', 'vagas': 'jobs', 'resposta': 'answer', 'respostas': 'answers',
  'resposta salva': 'saved answer', 'respostas salvas': 'saved answers', 'enviada': 'sent', 'enviadas': 'sent', 'testada': 'tested', 'testadas': 'tested',
  'vaga aguardando você': 'job waiting for you', 'vagas aguardando você': 'jobs waiting for you', 'pulada': 'skipped', 'puladas': 'skipped',
  'falha': 'failure', 'falhas': 'failures', 'pergunta em branco': 'blank question', 'perguntas em branco': 'blank questions',
  'aviso esperando': 'question waiting', 'avisos esperando': 'questions waiting', 'candidatura enviada': 'application sent', 'candidaturas enviadas': 'applications sent',
  'pergunta preenchida': 'question filled', 'perguntas preenchidas': 'questions filled', 'pergunta': 'question', 'perguntas': 'questions',
  'trecho': 'spot', 'trechos': 'spots', 'tentativa': 'attempt', 'tentativas': 'attempts', 'empresa': 'company', 'empresas': 'companies', 'palavra': 'word', 'palavras': 'words',
  'lista ganhou itens novos': 'list got new items', 'listas ganharam itens novos': 'lists got new items',

  // host: run log, reasons, errors
  'Execução interrompida': 'Run interrupted', 'Chrome fechado': 'Chrome closed', 'Entre no LinkedIn na janela do Chrome': 'Log in to LinkedIn in the Chrome window',
  'Login feito, sessão salva': 'Logged in, session saved', 'Entre no Indeed na janela do Chrome': 'Log in to Indeed in the Chrome window',
  'Login no Indeed feito, sessão salva': 'Logged in to Indeed, session saved', 'Candidatura enviada': 'Application sent',
  'Teste concluído, candidatura descartada': 'Test done, application discarded', 'Aguardando você: há avisos para responder': 'Waiting for you: there are questions to answer',
  'Preparando as respostas do formulário': 'Preparing the form answers', 'Na prioridade (país/idioma)': 'In priority (country/language)',
  'Fora da prioridade (país/idioma): fica para o fim': 'Outside priority (country/language): left for the end',
  'Filtro do Claude desligado: só empresas e palavras ignoradas filtram as vagas': 'Claude filter off: only ignored companies and words filter jobs',
  'A Ultra seleção precisa da IA para dar notas: desligue o modo sem IA ou use a execução normal': 'Ultra selection needs AI for scores: turn off no-AI mode or use a normal run',
  'Ultra seleção ainda não funciona no Indeed: execução normal': 'Ultra selection does not work on Indeed yet: normal run',
  'Análise interrompida a pedido: aplicando às vagas já qualificadas': 'Check stopped on request: applying to the jobs already qualified',
  'Não há mais vagas nesta lista do Indeed': 'No more jobs in this Indeed list',
  '5 falhas seguidas: o LinkedIn pode ter mudado a página. Execução parada.': '5 failures in a row: the site may have changed its page. Run stopped.',
  'Currículo: mantido o que já estava no LinkedIn': 'Resume: kept the one already on LinkedIn', 'Desmarcado: seguir empresa': 'Unchecked: follow company',
  'Desmarcado: e-mails com vagas': 'Unchecked: job e-mails', 'O LinkedIn bloqueou o Easy Apply por hoje (limite diário)': 'LinkedIn blocked Easy Apply for today (daily limit)',
  'Currículo não salvo: o LinkedIn aceita só PDF, DOC ou DOCX de até 2 MB': 'Resume not saved: LinkedIn accepts only PDF, DOC or DOCX up to 2 MB',
  'Perfil importado. Entre no LinkedIn de novo na primeira execução.': 'Profile imported. Log in to LinkedIn again on the first run.',
  'Claude indisponível: não deu para sugerir pesquisas. Tente mais tarde.': 'Claude unavailable: could not suggest searches. Try later.',
  'Esse link não é de um perfil do LinkedIn. Use o endereço que começa com linkedin.com/in/.': 'That link is not a LinkedIn profile. Use the address that starts with linkedin.com/in/.',
  'A leitura do perfil foi interrompida (Chrome fechado ou demorou demais). Tente de novo.': 'Reading the profile was interrupted (Chrome closed or took too long). Try again.',
  'O Claude não respondeu. Confira se o Claude Code está logado e tente de novo.': 'Claude did not answer. Check that Claude Code is logged in and try again.',
  'O Claude não respondeu. Confira se o Claude Code está logado (ou se o limite do plano acabou) e tente de novo.': 'Claude did not answer. Check that Claude Code is logged in (or whether the plan limit ran out) and try again.',
  'Não consegui ler a resposta do Claude. Tente de novo.': 'Could not read Claude’s answer. Try again.',
  'O Chrome do app está em uso. Pare a execução e tente de novo.': 'The app’s Chrome is in use. Stop the run and try again.',
  'você parou': 'you stopped it', 'interrompida': 'interrupted', 'fila concluída': 'queue done', 'a lista de vagas acabou': 'the job list ran out',
  'limite diário do LinkedIn': 'LinkedIn daily limit', 'Claude indisponível (limite de uso?)': 'Claude unavailable (usage limit?)', 'erro no navegador': 'browser error',
  '5 falhas seguidas': '5 failures in a row', 'começando': 'starting', 'enviando a 1 escolhida': 'sending the 1 picked',
  'sem candidatura simplificada': 'no easy apply', 'envio não confirmado': 'send not confirmed', 'login pedido no meio da candidatura': 'login asked mid-application',
  'Indeed': 'Indeed', 'LinkedIn': 'LinkedIn', 'Ultra': 'Ultra', 'agora': 'just now',
};

// Host-made lines and texts with variable parts. tr() runs on the captured parts that are themselves texts.
const EN_RX = [
  [/^Vaga aberta: (.+)$/, (_, j) => `Job opened: ${j}`],
  [/^Execução (em teste )?iniciada \((envio de verdade|nada é enviado)\)(, modo sem IA)?$/, (_, test, __, noAi) =>
    `${test ? 'Test run' : 'Run'} started (${test ? 'nothing is sent' : 'sending for real'})${noAi ? ', no-AI mode' : ''}`],
  [/^(Execução|Fila) concluída: (\d+) tentativas?$/, (_, w, n) => `${w === 'Fila' ? 'Queue' : 'Run'} finished: ${pl(n, 'attempt', 'attempts')}`],
  [/^(\d+) vagas? que falh(?:ou|aram) antes: tentando de novo$/, (_, n) => `${pl(n, 'job', 'jobs')} that failed before: trying again`],
  [/^Lista acabou: agora as (\d+) vagas? fora da prioridade de país\/idioma$/, (_, n) => `List ended: now the ${pl(n, 'job', 'jobs')} outside the country/language priority`],
  [/^(.+)\. Execução parada; tente amanhã\.$/, (_, e) => `${tr(e) ?? e}. Run stopped; try tomorrow.`],
  [/^(.+)\. Execução parada; a vaga atual fica para a próxima\. Tente de novo mais tarde\.$/, (_, e) => `${tr(e) ?? e}. Run stopped; the current job is left for next time. Try again later.`],
  [/^(.+)\. Análise parada: aplicando às vagas com respostas prontas$/, (_, e) => `${tr(e) ?? e}. Check stopped: applying to the jobs with answers ready`],
  [/^(.+)\. Tente mais tarde\.$/, (_, e) => `${tr(e) ?? e}. Try later.`],
  [/^Claude indisponível: (.*)$/, (_, d) => `Claude unavailable: ${d}`],
  [/^Ultra seleção: analisando até (\d+) vagas para enviar só as (\d+) melhores \(nota ≥ (\d+)\)$/, (_, a, b, c) => `Ultra selection: checking up to ${a} jobs to send only the best ${b} (score ≥ ${c})`],
  [/^Ultra seleção: (.+)$/, (_, x) => `Ultra selection: ${tr(x) ?? x}`],
  [/^(\d+) analisadas?, (\d+) no perfil, (\d+) com nota ≥ (\d+)(?: \(notas (\d+) a (\d+)\))?(?:; menos que (\d+)(: o Claude esgotou durante a análise|: análise interrompida antes de completar| porque as outras ficaram abaixo da nota))?$/,
    (_, a, b, c, d, lo, hi, less, why) => `${a} checked, ${b} fit the profile, ${c} scoring ≥ ${d}${lo ? ` (scores ${lo} to ${hi})` : ''}${less ? `; fewer than ${less}${{
      ': o Claude esgotou durante a análise': ': Claude ran out during the check', ': análise interrompida antes de completar': ': check stopped before completing' }[why] ?? ' because the others scored lower'}` : ''}`],
  [/^interrompida em (\d+) de (\d+) · (\d+) com nota ≥ (\d+); nada enviado$/, (_, a, b, c, d) => `stopped at ${a} of ${b} · ${c} scoring ≥ ${d}; nothing sent`],
  [/^Na disputa: nota (\d+), respostas prontas$/, (_, n) => `In the running: score ${n}, answers ready`],
  [/^Fora da disputa: nota (\d+)$/, (_, n) => `Out of the running: score ${n}`],
  [/^analisando (\d+) de (\d+)$/, (_, a, b) => `checking ${a} of ${b}`],
  [/^(\d+) com nota ≥ (\d+)$/, (_, a, b) => `${a} scoring ≥ ${b}`],
  [/^enviando as (\d+) escolhidas$/, (_, n) => `sending the ${n} picked`],
  [/^Lista da seção: (.*)$/, (_, k) => `Section list: ${k}`],
  [/^Não há mais vagas nesta lista \((.+)\)$/, (_, u) => `No more jobs in this list (${u})`],
  [/^Erro no navegador: (.*)$/, (_, e) => `Browser error: ${e}`],
  [/^Pulada: fora do perfil \((.*)\)$/, (_, w) => `Skipped: not a fit (${w})`],
  [/^(Pulada|Falhou): (.+)$/, (_, k, r) => `${k === 'Pulada' ? 'Skipped' : 'Failed'}: ${tr(r) ?? r}`],
  [/^fora do perfil: (.+)$/, (_, w) => `not a fit: ${w}`],
  [/^(empresa ignorada|palavra ignorada|fora do país|outro idioma) \((.+)\)$/, (_, k, v) =>
    `${{ 'empresa ignorada': 'ignored company', 'palavra ignorada': 'ignored word', 'fora do país': 'outside the country', 'outro idioma': 'other language' }[k]} (${v})`],
  [/^Etapa (\d+) concluída$/, (_, n) => `Step ${n} done`],
  [/^Claude: (\d+) perguntas? novas?$/, (_, n) => `Claude: ${pl(n, 'new question', 'new questions')}`],
  [/^Sem IA: (\d+) perguntas? novas? vir(?:a|am) avisos?$/, (_, n) => `No AI: ${pl(n, 'new question becomes', 'new questions become')} questions for you`],
  [/^Claude: nota (\d+)(?:, (.+))?$/, (_, n, w) => `Claude: score ${n}${w ? `, ${w}` : ''}`],
  [/^nota (\d+)$/, (_, n) => `score ${n}`],
  [/^Claude revisou (\d+) avisos? de (.+): (\d+) respondidos?$/, (_, n, j, k) => `Claude reviewed ${pl(n, 'question', 'questions')} of ${j}: ${k} answered`],
  [/^Currículo em (inglês|português): (.+) \((já estava no LinkedIn|enviado ao LinkedIn)\)$/, (_, l, f, how) =>
    `${l === 'inglês' ? 'English' : 'Portuguese'} resume: ${f} (${how.startsWith('já') ? 'already on LinkedIn' : 'uploaded to LinkedIn'})`],
  [/^Falhou ao enviar o currículo: (.*)$/, (_, e) => `Could not upload the resume: ${e}`],
  [/^Falhou ao preencher (.+?): (.*)$/, (_, f, e) => `Could not fill ${f}: ${e}`],
  [/^Perfil exportado para (.+)$/, (_, p) => `Profile exported to ${p}`],
  [/^Não deu para (exportar|importar): (.*)$/, (_, k, e) => `Could not ${k === 'exportar' ? 'export' : 'import'}: ${e}`],
  [/^Não consegui abrir o perfil no Chrome: (.*)$/, (_, e) => `Could not open the profile in Chrome: ${e}`],
  [/^A leitura do perfil falhou: (.*)$/, (_, e) => `Reading the profile failed: ${e}`],
  [/^chegou ao máximo \((\d+)\)$/, (_, n) => `reached the max (${n})`],
  [/^há (\d+) (min|h)$/, (_, n, u) => `${n} ${u} ago`],
  [/^Próxima vaga em (\d+) s$/, (_, n) => `Next job in ${n} s`],
  [/^Desde (\d\d:\d\d)( ·)?$/, (_, h, dot) => `Since ${h}${dot ?? ''}`],
  [/^(Última execução(?: \(teste\))?) · (.+)$/, (_, k, rest) => `${EN[k]} · ${tr(rest) ?? rest}`],
  [/^(\d+) de (\d+)$/, (_, a, b) => `${a} of ${b}`],
  [/^(Testar|Enviar) só (?:a 1 vaga|as (\d+) vagas) na fila$/, (_, k, n) => `${k === 'Testar' ? 'Test' : 'Send'} only the ${n ?? 1} queued ${n ? 'jobs' : 'job'}`],
  [/^Ver todos os (\d+) avisos$/, (_, n) => `See all ${n} questions`],
  [/^Não enviada: (\d+) avisos? esperando você$/, (_, n) => `Not sent: ${pl(n, 'question', 'questions')} waiting for you`],
  [/^O LinkedIn não mostrou o texto das (\d+) opções\. Digite como aparece no formulário, no idioma da vaga \(ex\.: Yes ou Sim\)\.$/, (_, n) =>
    `LinkedIn did not show the text of the ${n} options. Type it as it appears on the form, in the job’s language (e.g. Yes or Sim).`],
  [/^O LinkedIn recusou a resposta anterior: (.*)$/, (_, e) => `LinkedIn rejected the previous answer: ${e}`],
  [/^(Ver|Fechar) detalhes: (.+)$/, (_, k, t) => `${k === 'Ver' ? 'Show' : 'Hide'} details: ${t}`],
  [/^Abrir vaga no (LinkedIn|Indeed)$/, (_, s) => `Open job on ${s}`],
  [/^(Enviadas|Na fila|Puladas|Falhas): (\d+)$/, (_, k, n) => `${EN[k]}: ${n}`],
  [/^Excluída: (.+)$/, (_, q) => `Deleted: ${q}`],
  [/^usada em (.+)$/, (_, j) => `used in ${j}`],
  [/^Excluir resposta: (.+)$/, (_, q) => `Delete answer: ${q}`],
  [/^Resposta para: (.+)$/, (_, q) => `Answer to: ${q}`],
  [/^Resposta salva hoje: (.*)$/, (_, a) => `Saved answer today: ${a}`],
  [/^Salvo às (\d\d:\d\d)\.\s*(.*)$/, (_, h, rest) => `Saved at ${h}.${rest ? ' ' + (tr(rest) ?? rest) : ''}`],
  [/^Vale a partir da próxima execução\.$/, () => 'Applies from the next run on.'],
  [/^Digite (.+) para confirmar$/, (_, n) => `Type ${n} to confirm`],
  [/^Remover currículo em (português|inglês)$/, (_, l) => `Remove the ${l === 'inglês' ? 'English' : 'Portuguese'} resume`],
];

/** The English for one text, or null when there is none (then the text stays as it is). */
function tr(text) {
  const s = text.trim();
  if (!s) return null;
  const to = find(s);
  return to === s ? null : to; // same in both languages ("Normal", "Ok"): writing it back would loop the observer
}
function find(s) {
  if (EN[s] !== undefined) return EN[s];
  for (const [re, to] of EN_RX) if (re.test(s)) return s.replace(re, to);
  // Composite lines ("Vaga aberta · Dev .NET", "Desde 18:03 · ") translate part by part.
  if (s.includes(' · ')) {
    const parts = s.split(' · ').map(p => tr(p) ?? p);
    const out = parts.join(' · ');
    return out !== s ? out : null;
  }
  return null;
}

/** For app.js: a text built from pieces, with {name} slots. Portuguese stays as written. */
function t(text, vars) {
  const s = LANG === 'en' ? EN[text] ?? text : text;
  return vars ? s.replace(/\{(\w+)\}/g, (m, k) => k in vars ? vars[k] : m) : s;
}

const ATTRS = ['placeholder', 'aria-label', 'title', 'alt'];
const skip = el => el.closest?.('[translate="no"], script, style, textarea, code');

function translateNode(node) {
  if (node.nodeType === Node.TEXT_NODE) {
    if (node.parentElement && skip(node.parentElement)) return;
    const to = tr(node.data);
    if (to !== null) node.data = node.data.replace(node.data.trim(), to);
    return;
  }
  if (node.nodeType !== Node.ELEMENT_NODE) return;
  translateAttrs(node);
  if (skip(node)) return;
  for (const child of node.childNodes) translateNode(child);
}

function translateAttrs(el) {
  if (el.closest('[translate="no"]')) return; // a textarea's own placeholder is UI; only its typed text is the user's
  for (const a of ATTRS) {
    const v = el.getAttribute(a);
    const to = v && tr(v);
    if (to) el.setAttribute(a, to);
  }
}

const WATCH = { childList: true, subtree: true, characterData: true, attributes: true, attributeFilter: ATTRS };
// Off while it writes, so its own changes never come back as new mutations.
const observer = new MutationObserver(list => {
  observer.disconnect();
  for (const m of list) {
    if (m.type === 'childList') m.addedNodes.forEach(translateNode);
    else if (m.type === 'characterData') translateNode(m.target);
    else translateAttrs(m.target);
  }
  observer.observe(document.body, WATCH);
});

/** Called on every state. The first one sets the language before anything renders; a later switch reloads the
 *  window, so every text app.js already built comes back in the new language. */
let langSet = false;
function setLanguage(lang) {
  if (langSet) { if (lang !== LANG) location.reload(); return; }
  langSet = true;
  if (lang !== 'en') return;
  LANG = lang;
  document.documentElement.lang = 'en';
  translateNode(document.body);
  observer.observe(document.body, WATCH);
}
