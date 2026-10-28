/*
 Java Full Stack Development course seed for SQL Server / erpsystem.
 Idempotent seed: reuses active course/topics/contents and avoids duplicate assessment questions.
 Includes one starter notes chapter, one MCQ, one interview question and one practice task per module.
 Syllabus outline/days are from the supplied PDF; explanatory notes and assessment examples are generated starter content.
 BACK UP THE DATABASE FIRST. Review image URL and schema before executing.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRY
BEGIN TRANSACTION;
DECLARE @Now datetime2=SYSDATETIME(), @CourseId int, @CourseTitle nvarchar(100)=N'Java Full Stack Development';
DECLARE @CourseImage nvarchar(500)=N'https://tutorial.ciitstudent.com/Course_img/b0998bd1-f159-4388-94ea-66a4be8d1ab2.png';
SELECT TOP(1) @CourseId=course_id FROM erpsystem.tbltraining_courses WHERE course_name=@CourseTitle AND ISNULL(flag,0)=0 ORDER BY course_id;
IF @CourseId IS NULL
BEGIN
 INSERT erpsystem.tbltraining_courses(course_name,course_image_path,fees_amount,flag,InsertedAt,UpdatedAt,discount_mode,discount_value)
 VALUES(@CourseTitle,@CourseImage,8260,0,@Now,@Now,NULL,NULL);
 SET @CourseId=CONVERT(int,SCOPE_IDENTITY());
END
ELSE
 UPDATE erpsystem.tbltraining_courses SET fees_amount=8260,course_image_path=COALESCE(NULLIF(course_image_path,N''),@CourseImage),UpdatedAt=@Now WHERE course_id=@CourseId;

DECLARE @M TABLE(Seq int PRIMARY KEY,TopicName nvarchar(100),Days int,InterviewQ nvarchar(max),InterviewA nvarchar(max),ProgramTitle nvarchar(200),ProgramDesc nvarchar(max),McqQ nvarchar(max),O1 nvarchar(500),O2 nvarchar(500),O3 nvarchar(500),O4 nvarchar(500),Correct int);
INSERT @M VALUES
(1,N'HTML',10,N'Explain HTML elements versus attributes.',N'An element is the structure formed by tags and content; attributes configure an element in its opening tag.',N'Build an accessible student registration form.',N'Create labeled inputs for name/email, a course select, required validation, and a submit button. Use semantic form elements.',N'Which tag creates a hyperlink?',N'<a>',N'<div>',N'<link>',N'<href>',N'1'),
(2,N'CSS',8,N'Explain the CSS box model.',N'The box model consists of content, padding, border and margin. box-sizing:border-box includes padding and border in the declared width.',N'Style a responsive course card.',N'Use max-width, padding, border, margin:auto, box-sizing:border-box and a mobile breakpoint.',N'Which property adds space inside an element''s border?',N'margin',N'padding',N'outline',N'position',N'2'),
(3,N'JavaScript',15,N'Compare let, const and var.',N'let and const are block-scoped; var is function-scoped. const prevents reassignment of the binding.',N'Validate a form email field.',N'Trim input, reject empty values, validate format and return a clear message. Treat client validation as usability, not security.',N'Which method parses a JSON string?',N'JSON.stringify()',N'JSON.parse()',N'JSON.convert()',N'parse.JSON()',N'2'),
(4,N'jQuery',10,N'What does document.ready do in jQuery?',N'It runs a callback when the DOM can be safely queried and manipulated.',N'Add and remove table rows dynamically.',N'Bind click events, create elements safely, append to tbody, and delegate delete events for dynamically added rows.',N'Which jQuery method reads an input''s value?',N'text()',N'html()',N'val()',N'attr()',N'3'),
(5,N'Bootstrap',11,N'How does the Bootstrap grid work?',N'Containers hold rows; breakpoint-based columns adapt widths to viewport sizes.',N'Build a responsive course card listing.',N'Use .container, .row and .col-12 .col-md-6 .col-lg-4 with a card inside each column.',N'Which class creates a fluid full-width Bootstrap container?',N'container-fluid',N'row-fluid',N'grid-full',N'responsive-container',N'1'),
(6,N'React.js',23,N'Why should list items have stable keys?',N'Keys let React identify items between renders and reconcile insertions, removals and reordering.',N'Build a task list with add and complete actions.',N'Store tasks in useState, update immutably with map/filter, and use a stable task id as key.',N'Which hook manages local component state?',N'useEffect',N'useState',N'useContext',N'useMemo',N'2'),
(7,N'Core Java',20,N'Compare overloading and overriding.',N'Overloading uses the same method name with different parameters; overriding provides a subclass implementation of an inherited method.',N'Count character frequencies in a string.',N'Iterate through characters and use Map.merge(character, 1, Integer::sum); decide whether case and spaces matter.',N'Which keyword inherits a class in Java?',N'implements',N'extends',N'inherits',N'super',N'2'),
(8,N'Oracle SQL and PL/SQL',24,N'Compare WHERE and HAVING.',N'WHERE filters rows before grouping; HAVING filters groups after GROUP BY and can test aggregates.',N'Find departments with more than five employees.',N'Group by department_id and use HAVING COUNT(*) > 5.',N'Which attribute refers to a column''s data type in PL/SQL?',N'%ROWTYPE',N'%TYPE',N'%DATA',N'%COLUMN',N'2'),
(9,N'MySQL',17,N'What is a database index?',N'An index can speed up lookups but consumes storage and adds work to inserts, updates and deletes.',N'Calculate total order amount per customer.',N'Join customers and orders, group by customer, sum order amounts, and sort the total descending.',N'Which clause sorts query results?',N'GROUP BY',N'ORDER BY',N'SORT BY',N'RANK',N'2'),
(10,N'JDBC',10,N'What does Connection represent in JDBC?',N'It represents a session/connection to a database and is used to create statements and manage transactions.',N'Insert a student with JDBC.',N'Use try-with-resources, PreparedStatement parameters, executeUpdate and safe SQLException handling.',N'Why use PreparedStatement instead of SQL concatenation?',N'It helps prevent SQL injection and binds typed parameters separately from SQL syntax.',N'It encrypts all data automatically',N'It removes the need for a database',N'It replaces transactions',N'1'),
(11,N'JSP',25,N'What role does JSP play in a web application?',N'JSP renders dynamic server-side views; business logic should remain in controllers/services rather than the view.',N'Render a controller-provided list of students.',N'Place the list in request scope and render with JSTL; escape output to reduce XSS risk.',N'What does JSP stand for?',N'Java Server Pages',N'Java Standard Program',N'Java Servlet Process',N'Java Script Page',N'1'),
(12,N'Hibernate',11,N'What does ORM mean?',N'Object-relational mapping connects object models to relational tables and maps database rows to objects.',N'Map Department and Employee entities.',N'Use @OneToMany on Department and @ManyToOne on Employee; define the owning foreign-key side.',N'Which annotation identifies an entity primary key?',N'@Column',N'@Id',N'@EntityId',N'@Primary',N'2'),
(13,N'Spring Framework',11,N'What is dependency injection?',N'Dependencies are supplied externally rather than constructed directly, reducing coupling and improving testability.',N'Create a service with a repository dependency.',N'Inject the repository through the constructor and unit-test with a mock repository.',N'Which annotation commonly registers a service bean?',N'@Controller',N'@Service',N'@Table',N'@Injectable',N'2'),
(14,N'Spring MVC',17,N'What is the Model used for?',N'The Model carries attributes from a controller to a view for rendering.',N'Validate a course form and show errors.',N'Bind a request model, validate it, inspect BindingResult, return the form on errors and redirect after success.',N'Which annotation maps requests to controller methods?',N'@Entity',N'@RequestMapping',N'@Autowired',N'@BeanFactory',N'2'),
(15,N'Spring Boot',21,N'How does Spring Boot simplify setup?',N'Auto-configuration and starter dependencies provide sensible defaults based on the classpath and configuration.',N'Create a REST endpoint that creates a course.',N'Use a validated DTO, service layer, @PostMapping and ResponseEntity with HTTP 201 after successful creation.',N'Which annotation is typical on a Spring Boot entry class?',N'@SpringBootApplication',N'@SpringConfigurationOnly',N'@BootApp',N'@EnableWeb',N'1'),
(16,N'Microsoft SQL Server',17,N'What is a CTE?',N'A CTE is a named result set defined with WITH for one following statement and can support recursive queries.',N'Find each customer''s latest order.',N'Use ROW_NUMBER partitioned by customer_id and ordered by order_date descending, then filter to row number 1.',N'Which function numbers rows within a partition?',N'COUNT()',N'ROW_NUMBER()',N'RANKED()',N'NEXTROW()',N'2'),
(17,N'GitHub and Git',5,N'Compare Git and GitHub.',N'Git is a distributed version-control system; GitHub hosts Git repositories and collaboration workflows.',N'Create a feature branch and merge it safely.',N'Create branch, stage and commit changes, switch to main, merge and resolve conflicts after reviewing tests.',N'Which command creates and switches to a branch?',N'git branch -m',N'git switch -c',N'git merge -b',N'git push -c',N'2'),
(18,N'Jenkins',5,N'What is a Jenkins Pipeline?',N'A pipeline defines repeatable CI/CD stages such as checkout, build, test and deployment as code.',N'Create a pipeline that builds and tests a Java app.',N'Use Checkout, Build and Test stages; fail on non-zero exit codes and store secrets in Jenkins credentials.',N'Which file commonly defines a Jenkins Pipeline?',N'Dockerfile',N'Jenkinsfile',N'pom.lock',N'pipeline.yml only',N'2'),
(19,N'Azure',3,N'When choose App Service over a VM?',N'App Service is managed hosting; a VM offers more OS control but requires more administration.',N'Outline deployment to App Service.',N'Configure runtime and app settings, connect a deployment source/pipeline, then verify health and logs.',N'Which Azure service is managed web-app hosting?',N'Azure App Service',N'Azure Files only',N'Azure DNS',N'Azure Archive',N'1'),
(20,N'DevOps With Azure',0,N'How does Azure DevOps support a delivery workflow?',N'Azure DevOps provides repositories, pipelines, boards and artifact tooling to coordinate code changes and automated delivery.',N'Outline a CI pipeline for an Azure-hosted app.',N'Connect source control, build and test the app, publish an artifact, deploy to a test slot, and gate production release with approval and health checks.',N'Which Azure DevOps feature runs automated build and release steps?',N'Azure Pipelines',N'Azure DNS',N'Azure Files',N'Azure Monitor only',1),
(21,N'DevOps With AWS',0,N'What is the purpose of CI/CD in cloud delivery?',N'CI validates integrated changes automatically; CD automates delivering validated artifacts to target environments with repeatable controls.',N'Outline a basic deployment workflow to AWS.',N'Build and test on each change, store the artifact in a registry or artifact store, deploy through a controlled pipeline, and monitor health and rollback signals.',N'Which AWS service is commonly used to run a managed container image registry?',N'Amazon ECR',N'Amazon Route 53',N'AWS CloudTrail only',N'Amazon S3 Glacier',1),
(22,N'Docker',2,N'Compare Docker image and container.',N'An image is a packaged template; a container is an instance created from that image.',N'Containerize a small web application.',N'Choose a base image, copy dependencies and source, define startup command, then build and run with required ports only.',N'Which command builds an image from a Dockerfile?',N'docker start',N'docker build',N'docker pull',N'docker attach',N'2');
DECLARE @i int=1,@max int=(SELECT MAX(Seq) FROM @M),@TopicId int,@ContentId int,@ProgramId int;
DECLARE @Name nvarchar(100),@Days int,@IQ nvarchar(max),@IA nvarchar(max),@PT nvarchar(200),@PD nvarchar(max),@MQ nvarchar(max),@O1 nvarchar(500),@O2 nvarchar(500),@O3 nvarchar(500),@O4 nvarchar(500),@Correct int,@Html nvarchar(max);
WHILE @i<=@max
BEGIN
 SELECT @Name=TopicName,@Days=Days,@IQ=InterviewQ,@IA=InterviewA,@PT=ProgramTitle,@PD=ProgramDesc,@MQ=McqQ,@O1=O1,@O2=O2,@O3=O3,@O4=O4,@Correct=Correct FROM @M WHERE Seq=@i;
 SET @TopicId=NULL;
 SELECT TOP(1) @TopicId=topic_id FROM erpsystem.tbltraining_topics WHERE topic_name=@Name AND ISNULL(flag,0)=0 ORDER BY topic_id;
 IF @TopicId IS NULL
 BEGIN
  INSERT erpsystem.tbltraining_topics(topic_name,price,duration_minutes,flag,InsertedAt,UpdatedAt) VALUES(@Name,0,@Days*360,0,@Now,@Now);
  SET @TopicId=CONVERT(int,SCOPE_IDENTITY());
 END
 ELSE UPDATE erpsystem.tbltraining_topics SET duration_minutes=COALESCE(duration_minutes,@Days*360),UpdatedAt=@Now WHERE topic_id=@TopicId;
 IF NOT EXISTS(SELECT 1 FROM erpsystem.tbltraining_course_topics WHERE course_id=@CourseId AND topic_id=@TopicId AND ISNULL(flag,0)=0)
  INSERT erpsystem.tbltraining_course_topics(course_id,topic_id,flag,InsertedAt,UpdatedAt) VALUES(@CourseId,@TopicId,0,@Now,@Now);

 SET @ContentId=NULL;
 SELECT TOP(1) @ContentId=content_id FROM erpsystem.tbltraining_topic_contents WHERE topic_id=@TopicId AND content_name=N'Complete Module Notes' AND ISNULL(flag,0)=0 ORDER BY content_id;
 SET @Html=N'<article class="course-notes"><h2>'+@Name+N'</h2><p><strong>Suggested duration:</strong> '+CONVERT(nvarchar(10),@Days)+N' days.</p><h3>Syllabus checklist</h3><p>'+
 CASE @Name
 WHEN N'HTML' THEN N'Tags, elements, attributes, headings, paragraphs, links, images, lists, tables, forms, media, iframe, SVG, canvas, storage, drag and drop.'
              WHEN N'CSS' THEN N'Stylesheets, selectors, cascade, CSS3, responsive design, backgrounds, margin, padding, box model, layout.'
              WHEN N'JavaScript' THEN N'Variables, types, operators, input, conditions, switch, loops, arrays, JSON, date/time, cookies, localStorage, regex, validation, timers, callbacks.'
              WHEN N'jQuery' THEN N'Selectors, effects, DOM manipulation, html/text/val/css, events, AJAX, dimensions, forms, load, each, remove and clone.'
              WHEN N'Bootstrap' THEN N'Grid, image/button/table classes, icons, forms, cards, navbar, breadcrumbs, modal, tabs, carousel, pagination, accordion, badges, popovers, toasts, spinners.'
              WHEN N'React.js' THEN N'Components, props, hooks, events, lists/keys, JSON CRUD, forms, validation, styling, routing, Axios, error handling, React Bootstrap, lifecycle, deployment.'
              WHEN N'Core Java' THEN N'Compilation, OOP, types, operators, conditions, recursion, loops, arrays, strings, static members, access specifiers, constructors, exceptions, files, inheritance, interfaces, threads, collections, generics.'
              WHEN N'Oracle SQL and PL/SQL' THEN N'SELECT, WHERE, functions, grouping, joins, subqueries, set operators, DDL/DML, constraints, views, indexes, transactions, PL/SQL blocks, %TYPE, cursors, procedures, functions, triggers.'
              WHEN N'MySQL' THEN N'Installation, SQL, functions, grouping, joins, subqueries, DDL, constraints, views, indexes, transactions, stored procedures, functions and triggers.'
              WHEN N'JDBC' THEN N'Drivers, DriverManager, Connection, Statement, ResultSet, PreparedStatement, Oracle/MySQL connectivity, CRUD, stored procedures.'
              WHEN N'JSP' THEN N'JDK/JVM/JRE, Java fundamentals, Scanner, conditions, loops, arrays, strings, constructors, exceptions, files, Apache POI, OOP, threads, collections and JSP views.'
              WHEN N'Hibernate' THEN N'ORM architecture, HQL, XML and annotations, logging, entity relationships, transactions, named queries and demo project.'
              WHEN N'Spring Framework' THEN N'IoC, dependency injection, constructor injection, beans, autowiring, JdbcTemplate, ORM, CRUD and AOP.'
              WHEN N'Spring MVC' THEN N'MVC, controllers, views, models, ViewResolver, request parameters, forms, validation, uploads, CRUD, mail, exceptions, JSON and jQuery.'
              WHEN N'Spring Boot' THEN N'Auto-configuration, starters, embedded Tomcat, properties, logging, REST, CORS, email, uploads, JPA CRUD, frontend APIs, Thymeleaf, Eureka, Actuator, Kafka, Swagger, security and microservices.'
              WHEN N'Microsoft SQL Server' THEN N'SELECT, filters, TOP, functions, ranking, aggregates, grouping, joins, CTEs, DDL/DML, constraints, views, indexes, transactions, procedures, functions and triggers.'
              WHEN N'GitHub and Git' THEN N'Staging, commits, branches, stash, merge, pull/push, repositories, clone, conflicts and .gitattributes.'
              WHEN N'Jenkins' THEN N'Installation, Java/.NET build jobs, pipelines and automated build workflows.'
              WHEN N'Azure' THEN N'Cloud introduction, virtual machines, App Service and Azure DevOps.'
              WHEN N'DevOps With Azure' THEN N'Azure Repos, Pipelines, artifacts, deployment stages, approvals, app configuration, health checks and rollback.'
              WHEN N'DevOps With AWS' THEN N'Cloud CI/CD concepts, build and test stages, artifact storage, container registry, deployment automation, monitoring and rollback.'
              WHEN N'Docker' THEN N'Containers, images, installation, Dockerfile, build/run, ports and container lifecycle.'
 END+
 N'</p><h3>How to study</h3><ol><li>Read the concepts and write short notes.</li><li>Run examples in your own development environment.</li><li>Complete the practical task without copying a finished solution.</li><li>Attempt the MCQ and explain your reasoning.</li></ol><h3>Practice task</h3><p>'+@PD+N'</p><p><em>Starter course material generated for this course. Expand with classroom examples, diagrams and video links as needed.</em></p></article>';
 IF @ContentId IS NULL
 BEGIN
  INSERT erpsystem.tbltraining_topic_contents(topic_id,content_name,html_content,slides,video_name,flag,InsertedAt,UpdatedAt) VALUES(@TopicId,N'Complete Module Notes',@Html,NULL,NULL,0,@Now,@Now);
  SET @ContentId=CONVERT(int,SCOPE_IDENTITY());
 END
 ELSE UPDATE erpsystem.tbltraining_topic_contents SET html_content=CASE WHEN html_content IS NULL OR LTRIM(RTRIM(html_content))=N'' THEN @Html ELSE html_content END,UpdatedAt=@Now WHERE content_id=@ContentId;

 IF NOT EXISTS(SELECT 1 FROM erpsystem.tblcontent_questions WHERE content_id=@ContentId AND question=@MQ)
  INSERT erpsystem.tblcontent_questions(content_id,topic_id,note_id,question,option1,option2,option3,option4,correct_option_number,flag,InsertedAt,UpdatedAt) VALUES(@ContentId,@TopicId,@ContentId,@MQ,@O1,@O2,@O3,@O4,@Correct,0,@Now,@Now);
 IF NOT EXISTS(SELECT 1 FROM erpsystem.tblcontent_interview_questions WHERE content_id=@ContentId AND question=@IQ)
  INSERT erpsystem.tblcontent_interview_questions(content_id,topic_id,note_id,question,answer,flag,InsertedAt,UpdatedAt) VALUES(@ContentId,@TopicId,@ContentId,@IQ,@IA,0,@Now,@Now);
 SET @ProgramId=NULL;
 SELECT TOP(1) @ProgramId=program_question_id FROM erpsystem.tblcontent_program_questions WHERE content_id=@ContentId AND question_title=@PT AND ISNULL(flag,0)=0;
 IF @ProgramId IS NULL
 BEGIN
  INSERT erpsystem.tblcontent_program_questions(content_id,note_id,question_title,question_description,flag,InsertedAt,UpdatedAt) VALUES(@ContentId,@ContentId,@PT,@PD,0,@Now,@Now);
  SET @ProgramId=CONVERT(int,SCOPE_IDENTITY());
 END
 IF NOT EXISTS(SELECT 1 FROM erpsystem.tblcontent_program_answers WHERE program_question_id=@ProgramId AND program_answer=@PD)
  INSERT erpsystem.tblcontent_program_answers(program_question_id,program_answer,program_description,flag,InsertedAt,UpdatedAt) VALUES(@ProgramId,@PD,N'Suggested approach: break the task into small steps, validate inputs, handle edge cases, and test with normal and boundary values.',0,@Now,@Now);
 SET @i+=1;
END;
COMMIT TRANSACTION;
SELECT @CourseId AS CourseId,@CourseTitle AS CourseName,COUNT(DISTINCT t.topic_id) AS ModuleCount,COUNT(DISTINCT c.content_id) AS NotesCount
FROM erpsystem.tbltraining_course_topics cm
JOIN erpsystem.tbltraining_topics t ON t.topic_id=cm.topic_id
LEFT JOIN erpsystem.tbltraining_topic_contents c ON c.topic_id=t.topic_id AND c.content_name=N'Complete Module Notes' AND ISNULL(c.flag,0)=0
WHERE cm.course_id=@CourseId AND ISNULL(cm.flag,0)=0;
END TRY
BEGIN CATCH
 IF @@TRANCOUNT>0 ROLLBACK TRANSACTION;
 THROW;
END CATCH;
